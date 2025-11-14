// Simple retry delegating handler to avoid bringing in Polly for now
internal class SimpleRetryHandler : DelegatingHandler
{
    // allow a configurable number of attempts; default to 3 for transient network resilience
    private readonly int _maxAttempts;

    private readonly ILogger<SimpleRetryHandler>? _log;

    public SimpleRetryHandler() : this(maxAttempts: 3)
    {
    }

    // Allow DI to inject an ILogger when this handler is registered by the HttpClient factory
    public SimpleRetryHandler(ILogger<SimpleRetryHandler> log) : this(log, maxAttempts: 3)
    {
    }

    // Provide constructors to configure max attempts
    public SimpleRetryHandler(int maxAttempts)
    {
        _maxAttempts = Math.Max(1, maxAttempts);
    }

    public SimpleRetryHandler(ILogger<SimpleRetryHandler> log, int maxAttempts)
    {
        _log = log;
        _maxAttempts = Math.Max(1, maxAttempts);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _log?.LogDebug("SimpleRetryHandler starting SendAsync: Method={Method} Uri={Uri} MaxAttempts={MaxAttempts} RequestVersion={Version} CanCancel={CanCancel}",
            request.Method, request.RequestUri, _maxAttempts, request.Version, cancellationToken.CanBeCanceled);

        // Buffer content once so we can recreate the request for retries
        byte[]? bufferedContent = null;
        System.Net.Http.Headers.MediaTypeHeaderValue? contentType = null;
        if (request.Content != null)
        {
            try
            {
                bufferedContent = await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                contentType = request.Content.Headers.ContentType;
            }
            catch (Exception ex)
            {
                _log?.LogWarning(ex, "Failed to buffer request content for {Uri}; proceeding without buffered content", request.RequestUri);
            }
        }

        for (int attempt = 0; attempt < _maxAttempts; attempt++)
        {
            // create a fresh HttpRequestMessage for each attempt
            using var requestClone = CloneRequest(request, bufferedContent, contentType);

            try
            {
                _log?.LogDebug("Attempt {Attempt} sending request to {Uri} (RequestVersion={Version}) - callerCanceled={CallerCanceled}", attempt + 1, requestClone.RequestUri, requestClone.Version, cancellationToken.IsCancellationRequested);
                var sw = System.Diagnostics.Stopwatch.StartNew();

                var response = await base.SendAsync(requestClone, cancellationToken).ConfigureAwait(false);

                sw.Stop();
                _log?.LogInformation("Request completed. Method={Method} Uri={Uri} Status={Status} Attempt={Attempt} DurationMs={Ms}", requestClone.Method, requestClone.RequestUri, (int)response.StatusCode, attempt + 1, sw.ElapsedMilliseconds);

                // treat 5xx as transient
                if ((int)response.StatusCode >= 500 && attempt < _maxAttempts - 1)
                {
                    _log?.LogWarning("Transient server error (status {Status}) on attempt {Attempt} for {Uri}; will retry", (int)response.StatusCode, attempt + 1, requestClone.RequestUri);
                    response.Dispose();
                    if (cancellationToken.IsCancellationRequested)
                        cancellationToken.ThrowIfCancellationRequested();

                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return response;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < _maxAttempts - 1)
            {
                // Log detailed cancellation/transport state to help diagnose whether cancellation is local or remote
                _log?.LogWarning(ex, "Transient exception on attempt {Attempt} for {Uri}; will retry. CallerCanceled={CallerCanceled} ExceptionType={ExType} Inner={Inner}",
                    attempt + 1, request.RequestUri, cancellationToken.IsCancellationRequested, ex.GetType().Name, ex.InnerException?.GetType().Name);

                // if caller already cancelled, propagate original cancellation instead of retrying
                if (cancellationToken.IsCancellationRequested)
                    throw;

                // jittered backoff to avoid stampeding
                var delayMs = (int)(Math.Pow(2, attempt) * 1000);
                var jitter = new Random().Next(0, 250);
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs + jitter), cancellationToken).ConfigureAwait(false);
                continue;
            }
            catch (Exception ex)
            {
                _log?.LogError(ex, "Request failed for {Uri} on attempt {Attempt}; not retrying. CallerCanceled={CallerCanceled} ExceptionType={ExType}", request.RequestUri, attempt + 1, cancellationToken.IsCancellationRequested, ex.GetType().Name);
                throw;
            }
        }

        // last attempt - use a fresh clone
        using var finalRequest = CloneRequest(request, bufferedContent, contentType);
        _log?.LogDebug("Performing final attempt to {Uri}", finalRequest.RequestUri);
        return await base.SendAsync(finalRequest, cancellationToken).ConfigureAwait(false);
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage original, byte[]? contentBytes, System.Net.Http.Headers.MediaTypeHeaderValue? contentType)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version
        };

        // copy request headers
        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        // copy content (buffered) and content headers
        if (contentBytes != null)
        {
            var content = new ByteArrayContent(contentBytes);
            if (contentType != null)
                content.Headers.ContentType = contentType;

            // copy other content headers
            if (original.Content != null)
            {
                foreach (var h in original.Content.Headers)
                {
                    if (!string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                        content.Headers.TryAddWithoutValidation(h.Key, h.Value);
                }
            }

            clone.Content = content;
        }

#if NET6_0_OR_GREATER
        // copy Options if present (best-effort)
        foreach (var prop in original.Options)
        {
            // Use object? generic parameter to match HttpRequestOptionsKey<TValue?> signature and avoid nullability mismatch
            var key = new System.Net.Http.HttpRequestOptionsKey<object?>(prop.Key);
            clone.Options.Set(key, prop.Value);
        }
#endif

        return clone;
    }

    private static bool IsTransient(Exception ex)
    {
        // treat typical network exceptions as transient.
        // Do NOT treat TaskCanceledException as transient by default because it may represent a timeout
        // or a disposed transport which should not be blindly retried.
        if (ex is HttpRequestException || ex is IOException)
            return true;

        // consider HttpRequestException.InnerException containing SocketException transient as well
        if (ex is HttpRequestException hre && hre.InnerException is System.Net.Sockets.SocketException)
            return true;

        return false;
    }
}