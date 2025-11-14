using CodeHero.ApiService.Contracts;
using CodeHero.ApiService.Utilities;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeHero.ApiService.Services.Rag;

/// <summary>
/// Rephrases user follow-up questions into a standalone, repository-grounded question using Azure OpenAI chat completions.
/// It should be used to take a user's follow-up input and conversation history, and produce a self-contained question.
/// with relevant context from the chat history, before passing it to a retrieval-augmented generation (RAG) system.
/// </summary>
/// <remarks>
/// This implementation attempts deterministic fallbacks for trivial inputs or when the model requests clarification.
/// It prefers an <see cref="IHttpClientFactory"/>-provided client named "foundry" if available; otherwise it creates a local <see cref="HttpClient"/>.
/// The class is intended for use in request-scoped services; dependencies are provided via DI.
/// </remarks>
public sealed class QuestionRephraser : IQuestionRephraser
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<QuestionRephraser> _log;

    /// <summary>
    /// Regex matching phrases that typically indicate the model is asking for clarification.
    /// If the returned choice matches this pattern, a deterministic fallback is used.
    /// </summary>
    private static readonly Regex ClarifyPattern = new(@"\b(clarify|clarification|more details|could you|please provide|what do you mean|can you explain|provide more|could you clarify)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Set of common trivial single-word inputs that should be handled deterministically
    /// (for example "help", "hi", "thanks") to avoid unnecessary calls to the LLM.
    /// </summary>
    private static readonly HashSet<string> TrivialInputs = new(StringComparer.OrdinalIgnoreCase)
    {
        "help","hi","hello","bye","thanks","thank you","info","info?"
    };

    /// <summary>
    /// Mapping of number words to integers used to parse spelled-out requirement numbers from conversation history.
    /// </summary>
    private static readonly Dictionary<string, int> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"] = 0,
        ["one"] = 1,
        ["two"] = 2,
        ["three"] = 3,
        ["four"] = 4,
        ["five"] = 5,
        ["six"] = 6,
        ["seven"] = 7,
        ["eight"] = 8,
        ["nine"] = 9,
        ["ten"] = 10
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionRephraser"/> class.
    /// </summary>
    /// <param name="http">Factory to create <see cref="HttpClient"/> instances; may be null in some test scenarios.</param>
    /// <param name="cfg">Application configuration used to obtain AOAI endpoint, key, deployment and API version settings.</param>
    /// <param name="log">Logger instance for diagnostic and telemetry messages.</param>
    public QuestionRephraser(IHttpClientFactory http, IConfiguration cfg, ILogger<QuestionRephraser> log)
    {
        _http = http;
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(log);
        _cfg = cfg;
        _log = log;
    }

    /// <summary>
    /// Rephrases the supplied chat follow-up input into a single, self-contained question grounded in the repository context.
    /// </summary>
    /// <param name="request">The chat request containing the follow-up input and conversation history.</param>
    /// <param name="ct">Cancellation token to observe for request cancellation.</param>
    /// <returns>
    /// A task that resolves to the standalone question string. In error or cancellation cases, the original <see cref="ChatRequest.ChatInput"/> is returned.
    /// </returns>
    /// <remarks>
    /// Behavior:
    /// - If the input is considered trivial, returns a deterministic grounded question without calling the LLM.
    /// - Otherwise, calls the configured Azure OpenAI chat completions endpoint with a system/user prompt and returns the model's first choice text.
    /// - If the model responds with a clarification request or an empty response, a best-effort deterministic fallback is returned.
    /// - The HTTP client uses an "api-key" header for authentication; configuration keys are read from <see cref="IConfiguration"/>.
    /// </remarks>
    public async Task<string> RephraseAsync(ChatRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var followRaw = (request.ChatInput ?? string.Empty).Trim();

        var system = """
        You are a concise rephraser. Given the conversation history and the user's follow-up, produce a single, self-contained question that preserves the user's intent.
        Always ground the standalone question in the application's repository: use the codebase, docs (docs/*), and architecture diagrams as the context. If the user's follow-up is generic, reframe it specifically for this application (for example: 'For this codebase, how would you ...').
        NEVER ask the user for clarification. If the model would ask a clarification question, instead RETURN the best-effort standalone question using the available conversation history and the follow-up input. If insufficient context exists, return the original follow-up input.
        Output only the standalone question (no commentary, no prefixes like 'Question:' or 'Follow-up:'). Keep it brief and do not add extra details beyond what's necessary to make the question standalone.
        """;

        var historyItems = request.ChatHistory ?? Array.Empty<ChatTurn>();
        var history = string.Join("\n", historyItems.Select(h =>
            $"user:\n{h.User}\nassistant:\n{h.Assistant}"));

        var user = $"""
        chat history:
        {history}

        Follow up Input: {request.ChatInput}
        Standalone Question:
        """;

        // Use configured Foundry (AzureAI) settings only; AOAI keys are not used in this project
        var endpoint = _cfg["AzureAI:Foundry:Endpoint"] ?? string.Empty;
        var key = _cfg["AzureAI:Foundry:Key"] ?? _cfg["AzureAI:Foundry:ApiKey"] ?? string.Empty;
        var deployment = _cfg["AzureAI:Foundry:PhiDeployment"] ?? "Phi-4";
        var apiVersion = _cfg["AzureAI:Foundry:ApiVersion"] ?? "2024-08-01-preview";

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
        {
            _log!.LogWarning("Rephraser missing endpoint/key configuration.");
            return request.ChatInput ?? string.Empty;
        }

        var url = endpoint.TrimEnd('/') + $"/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";
        var payload = new
        {
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            },
            temperature = 0.0,
            max_tokens = 256
        };

        HttpClient? client = null;
        bool disposeClient = false;
        try
        {
            // Prefer factory-created client (so tests can inject a stubbed client). If factory client unavailable, fall back to direct SocketsHttpHandler.
            try
            {
                client = _http?.CreateClient("foundry");
                if (client is not null)
                {
                    try { client.Timeout = Timeout.InfiniteTimeSpan; } catch { }
                    client.DefaultRequestHeaders.Remove("api-key");
                    client.DefaultRequestHeaders.Add("api-key", key);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                }
            }
            catch (Exception ex)
            {
                _log!.LogDebug(ex, "QuestionRephraser: IHttpClientFactory CreateClient failed; will create local handler client.");
                client = null;
            }

            if (client is null)
            {
                var handler = new SocketsHttpHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                    Expect100ContinueTimeout = TimeSpan.Zero,
                    UseProxy = false
                };
                client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
                client.DefaultRequestVersion = HttpVersion.Version11;
                client.DefaultRequestHeaders.Remove("api-key");
                client.DefaultRequestHeaders.Add("api-key", key);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                disposeClient = true;
            }

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            req.Headers.ConnectionClose = true;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            sw.Stop();
            _log!.LogInformation("QuestionRephraser sending request id={Id} url={Url} status={Status} elapsedMs={Ms}", Guid.NewGuid().ToString("D"), url, resp.StatusCode, sw.ElapsedMilliseconds);

            if (!resp.IsSuccessStatusCode)
            {
                _log!.LogWarning("Rephrase call failed status={Status} bodySnippet={Snippet}", (int)resp.StatusCode, body.Length > 300 ? body.Substring(0, 300) : body);
                return request.ChatInput ?? string.Empty;
            }

            // Use centralized parser
            var parsed = OpenAiResponseParser.TryGetFirstChoiceContent(body);
            var choiceText = (parsed ?? body ?? string.Empty).Trim();

            // If model asked for clarification, override with best-effort deterministic fallback
            if (string.IsNullOrWhiteSpace(choiceText) || ClarifyPattern.IsMatch(choiceText ?? string.Empty))
            {
                _log!.LogInformation("QuestionRephraser returned clarification-like text; applying deterministic fallback. RawChoiceLength={Len}", choiceText?.Length ?? 0);
                var fallback = BuildBestEffortStandalone(request);
                _log!.LogInformation("QuestionRephraser fallback choice='{Choice}'", fallback);
                return string.IsNullOrWhiteSpace(fallback) ? (request.ChatInput ?? string.Empty) : fallback;
            }

            var trimmed = (choiceText ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? request.ChatInput ?? string.Empty : trimmed;
        }
        catch (TaskCanceledException tex)
        {
            _log!.LogWarning(tex, "Rephrase call canceled; returning original input.");
            return request.ChatInput ?? string.Empty;
        }
        catch (Exception ex)
        {
            _log!.LogError(ex, "Rephrase call threw; returning original input.");
            return request.ChatInput ?? string.Empty;
        }
        finally
        {
            if (disposeClient && client is not null)
                client.Dispose();
        }
    }

    /// <summary>
    /// Attempts to produce a best-effort standalone question by heuristically combining history and follow-up input.
    /// </summary>
    /// <param name="request">Chat request containing history and the follow-up.</param>
    /// <returns>
    /// A standalone question string if one can be constructed deterministically; otherwise an empty string
    /// (callers typically fall back to the original follow-up when empty).
    /// </returns>
    private static string BuildBestEffortStandalone(ChatRequest request)
    {
        // Previous heuristic: combine last user and follow-up
        try
        {
            var lastUser = request.ChatHistory?.Where(h => !string.IsNullOrWhiteSpace(h.User)).Select(h => h.User.Trim()).LastOrDefault();
            var follow = (request.ChatInput ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(lastUser) && !string.Equals(lastUser, follow, StringComparison.OrdinalIgnoreCase))
            {
                var combined = (lastUser + " " + follow).Trim();
                if (!combined.EndsWith("?")) combined += "?";
                return combined;
            }

            if (!string.IsNullOrWhiteSpace(follow))
            {
                return follow.EndsWith("?") ? follow : (follow + "?");
            }

            return string.Empty;
        }
        catch
        {
            return request.ChatInput ?? string.Empty;
        }
    }

    /// <summary>
    /// Searches the conversation history for requirement identifiers (e.g. "REQ-003" or "requirement 3") and returns a normalized ID.
    /// </summary>
    /// <param name="request">The chat request whose history to inspect.</param>
    /// <returns>
    /// A normalized requirement identifier like "REQ-003" if found; otherwise <c>null</c>.
    /// </returns>
    private static string? FindRequirementIdInHistory(ChatRequest request)
    {
        // Search both chat history user and assistant text for patterns like REQ-003 or 'requirement 3'
        if (request?.ChatHistory is null) return null;

        // 1) direct REQ-?0*(\d{1,4}) pattern
        var reqPattern = new Regex(@"REQ-?0*(\d{1,4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        foreach (var turn in request.ChatHistory.Reverse())
        {
            var m1 = reqPattern.Match(turn.User ?? string.Empty);
            if (m1.Success) return FormatReqId(m1.Groups[1].Value);
            m1 = reqPattern.Match(turn.Assistant ?? string.Empty);
            if (m1.Success) return FormatReqId(m1.Groups[1].Value);
        }

        // 2) 'requirement' followed by digits or number words
        var reqWord = new Regex(@"requirement\s+([0-9]{1,4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        foreach (var turn in request.ChatHistory.Reverse())
        {
            var m2 = reqWord.Match(turn.User ?? string.Empty);
            if (m2.Success) return FormatReqId(m2.Groups[1].Value);
            m2 = reqWord.Match(turn.Assistant ?? string.Empty);
            if (m2.Success) return FormatReqId(m2.Groups[1].Value);
        }

        // 3) spelled number words after 'requirement'
        var reqWordWord = new Regex(@"requirement\s+([a-z]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        foreach (var turn in request.ChatHistory.Reverse())
        {
            var m3 = reqWordWord.Match(turn.User ?? string.Empty);
            if (m3.Success)
            {
                var w = m3.Groups[1].Value.ToLowerInvariant();
                if (NumberWords.TryGetValue(w, out var v)) return FormatReqId(v.ToString());
            }
            m3 = reqWordWord.Match(turn.Assistant ?? string.Empty);
            if (m3.Success)
            {
                var w = m3.Groups[1].Value.ToLowerInvariant();
                if (NumberWords.TryGetValue(w, out var v)) return FormatReqId(v.ToString());
            }
        }

        return null;
    }

    /// <summary>
    /// Formats a numeric requirement identifier into the canonical "REQ-###" form.
    /// </summary>
    /// <param name="digits">String containing digits (or other characters) to normalize.</param>
    /// <returns>Normalized requirement id, or the original input if normalization fails.</returns>
    private static string FormatReqId(string digits)
    {
        var num = new string(digits.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(num)) return digits;
        if (int.TryParse(num, out var n))
            return $"REQ-{n:D3}";
        return digits;
    }
}