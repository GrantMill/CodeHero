using Azure;
using Azure.Search.Documents;
using CodeHero.ApiService;
using CodeHero.ApiService.Contracts;
using CodeHero.ApiService.Services.Rag;
using CodeHero.ApiService.Utilities;
using CodeHero.Extensions;
using CodeHero.Services;
using Polly;
using Polly.Extensions.Http;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// after builder.Configuration is available
builder.Services.AddOptionalAzureSearchIndexer(builder.Configuration);
builder.Services.AddSingleton<IAzureSearchClientFactory, DefaultAzureSearchClientFactory>();
builder.Services.AddSingleton<ISearchIndexerService, AzureSearchIndexerService>();

// Register background indexer
builder.Services.AddSingleton<IBackgroundIndexer, BackgroundIndexerService>();
builder.Services.AddHostedService(sp => (BackgroundIndexerService)sp.GetRequiredService<IBackgroundIndexer>());

// RAG: Search client (optional)
builder.Services.AddHttpClient();
// Configure a named HttpClient for AOAI/Foundry calls with tuned timeouts and a simple retry handler
builder.Services.AddTransient<SimpleRetryHandler>();
builder.Services.AddTransient<FoundryPolicyHandler>();

// Read resilience settings from configuration (fallback defaults)
var foundryAttemptTimeoutSec = builder.Configuration.GetValue<int?>("Resilience:FoundryAttemptTimeoutSeconds") ?? 120;
var foundryRetryCount = builder.Configuration.GetValue<int?>("Resilience:FoundryRetryCount") ?? 3;
var foundryCircuitFailures = builder.Configuration.GetValue<int?>("Resilience:FoundryCircuitFailures") ?? 5;
var foundryCircuitDurationSec = builder.Configuration.GetValue<int?>("Resilience:FoundryCircuitDurationSeconds") ?? 60;

builder.Services.AddHttpClient("foundry", client =>
{
    // prefer HTTP/1.1 to avoid HTTP/2 transport/keepalive quirks
    client.DefaultRequestVersion = HttpVersion.Version11;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;

    // allow long running completions and let callers cap runtime via CancellationToken
    client.Timeout = Timeout.InfiniteTimeSpan;
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    // Reduce chance of stale/closed connections being reused
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
})
.AddHttpMessageHandler<SimpleRetryHandler>()
.AddHttpMessageHandler<FoundryPolicyHandler>();

// Ensure the named 'foundry' client does not inherit global resilience/service-discovery handlers
builder.Services.Configure<Microsoft.Extensions.Http.HttpClientFactoryOptions>("foundry", options =>
{
    options.HttpMessageHandlerBuilderActions.Clear();
});

// Register default SearchClient factory / instance
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var searchEndpoint = cfg["Search:Endpoint"] ?? cfg["AzureSearch:Endpoint"];
    var searchKey = cfg["Search:Key"] ?? cfg["Search:ApiKey"] ?? cfg["AzureSearch:ApiKey"];
    var indexName = cfg["Search:IndexName"] ?? cfg["AzureSearch:IndexName"] ?? "codehero-docs";
    if (string.IsNullOrWhiteSpace(searchEndpoint) || string.IsNullOrWhiteSpace(searchKey))
        return null; // not configured
    return new SearchClient(new Uri(searchEndpoint), indexName, new AzureKeyCredential(searchKey));
});

builder.Services.AddScoped<IQuestionRephraser, QuestionRephraser>();
builder.Services.AddScoped<IHybridSearchService>(sp =>
{
    var sc = sp.GetService<SearchClient>();
    if (sc is null)
        return new FakeHybridSearchService();
    return new HybridSearchService(sc, sp.GetRequiredService<IHttpClientFactory>(), sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<ILogger<HybridSearchService>>());
});
builder.Services.AddScoped<IRagAnswerService, RagAnswerService>();

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Startup inspector: log foundry client options and ensure no global builder actions remain
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Startup.FoundryInspector");
    try
    {
        var optionsMonitor = sp.GetService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.Extensions.Http.HttpClientFactoryOptions>>();
        if (optionsMonitor is not null)
        {
            var opt = optionsMonitor.Get("foundry");
            logger.LogInformation("Foundry HttpClientFactoryOptions: HttpMessageHandlerBuilderActions.Count={Count}", opt.HttpMessageHandlerBuilderActions?.Count ?? 0);
            // re-clear to be extra safe
            opt.HttpMessageHandlerBuilderActions?.Clear();
            logger.LogInformation("Cleared Foundry HttpMessageHandlerBuilderActions; Count now={Count}", opt.HttpMessageHandlerBuilderActions?.Count ?? 0);
        }
        else
        {
            logger.LogWarning("IOptionsMonitor<HttpClientFactoryOptions> not available to inspect.");
        }

        // Log the named client defaults if possible
        var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
        var client = httpFactory.CreateClient("foundry");
        logger.LogInformation("Named 'foundry' HttpClient constructed: Timeout={Timeout} DefaultRequestVersion={Version} DefaultVersionPolicy={Policy} BaseAddress={Base}", client.Timeout, client.DefaultRequestVersion, client.DefaultVersionPolicy, client.BaseAddress);
    }
    catch (Exception ex)
    {
        var logger2 = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Startup.FoundryInspector");
        logger2.LogError(ex, "Error inspecting foundry client at startup");
    }
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// RAG endpoints
app.MapPost("/api/chat/rephrase", async (ChatRequest req, IQuestionRephraser rephraser, CancellationToken ct) =>
    Results.Ok(await rephraser.RephraseAsync(req, ct)));

app.MapPost("/api/search/hybrid", async (SearchRequest req, IHybridSearchService search, CancellationToken ct) =>
    Results.Ok(await search.SearchAsync(req, ct)));

app.MapPost("/api/chat/answer", async (AnswerRequest req, IRagAnswerService rag, CancellationToken ct) =>
    Results.Ok(await rag.AnswerAsync(req, ct)));

// Orchestrated agent chat endpoint: accept plain-text POST body and route to MCP orchestrator if applicable
// otherwise run the RAG pipeline (rephrase -> search -> answer) and return plain-text output.
app.MapPost("/api/agent/chat", async (HttpRequest httpReq, IQuestionRephraser rephraser, IHybridSearchService search, IRagAnswerService rag, ILoggerFactory loggerFactory, CancellationToken ct) =>
{
    var logger = loggerFactory.CreateLogger("AgentChatEndpoint");
    string text;
    using (var sr = new StreamReader(httpReq.Body))
    {
        text = await sr.ReadToEndAsync(ct);
    }
    text = text?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(text)) return Results.BadRequest("empty input");

    // Create a linked CTS with the configured foundry attempt timeout so long-running RAG/Foundry calls
    // aren't prematurely cancelled by shorter upstream timeouts. This gives the pipeline a consistent cap.
    var linked = CancellationHelper.CreateLinkedCtsWithId(ct, TimeSpan.FromSeconds(foundryAttemptTimeoutSec));
    using var linkedCts = linked.Cts;
    var linkedId = linked.Id;
    var token = linkedCts.Token;

    try
    {
        // First attempt: quick heuristic for MCP list/read intents to avoid RAG call
        var norm = text.ToLowerInvariant();
        if (norm.Contains("list") && norm.Contains("req"))
        {
            // Try to call IMcpClient.CallRawAsync dynamically to avoid compile-time dependency on Web project types
            var svc = httpReq.HttpContext.RequestServices.GetService(typeof(object));
            if (svc is not null)
            {
                var callRaw = svc.GetType().GetMethod("CallRawAsync", new Type[] { typeof(string), typeof(object), typeof(CancellationToken) });
                if (callRaw is not null)
                {
                    var task = (Task<string>?)callRaw.Invoke(svc, new object[] { "fs/list", new { root = "requirements", exts = new[] { ".md" } }, token });
                    if (task is not null)
                    {
                        var raw = await task;
                        try
                        {
                            using var doc = JsonDocument.Parse(raw);
                            if (doc.RootElement.TryGetProperty("result", out var result) && result.TryGetProperty("files", out var filesEl) && filesEl.ValueKind == JsonValueKind.Array)
                            {
                                var files = filesEl.EnumerateArray().Select(e => e.GetString()).Where(s => !string.IsNullOrEmpty(s));
                                return Results.Ok(string.Join("\n", files!));
                            }
                        }
                        catch { /* fall through to RAG */ }
                    }
                }
            }
        }

        // Otherwise: RAG pipeline with per-stage diagnostics
        var chatReq = new ChatRequest(text, Array.Empty<ChatTurn>());

        var swRephrase = System.Diagnostics.Stopwatch.StartNew();
        string? standalone = null;
        try
        {
            standalone = await rephraser.RephraseAsync(chatReq, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            logger.LogWarning("AgentChat: rephrase canceled after {Ms}ms (caller token).", swRephrase.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AgentChat: rephrase failed after {Ms}ms", swRephrase.ElapsedMilliseconds);
            throw;
        }
        finally { swRephrase.Stop(); }
        logger.LogInformation("AgentChat: rephrase -> {Standalone} (ms={Ms})", standalone, swRephrase.ElapsedMilliseconds);

        var swSearch = System.Diagnostics.Stopwatch.StartNew();
        SearchResponse? searchResp = null;
        try
        {
            var searchReq = new SearchRequest(standalone ?? text, TopK: 6);
            searchResp = await search.SearchAsync(searchReq, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            logger.LogWarning("AgentChat: search canceled after {Ms}ms (caller token).", swSearch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AgentChat: search failed after {Ms}ms", swSearch.ElapsedMilliseconds);
            throw;
        }
        finally { swSearch.Stop(); }
        var contexts = searchResp?.Results ?? Array.Empty<SearchHit>();
        logger.LogInformation("AgentChat: search returned {Count} contexts (ms={Ms})", contexts.Count(), swSearch.ElapsedMilliseconds);

        var swAnswer = System.Diagnostics.Stopwatch.StartNew();
        AnswerResponse? answer = null;
        try
        {
            var answerReq = new AnswerRequest(text, Array.Empty<ChatTurn>(), contexts);
            answer = await rag.AnswerAsync(answerReq, token);
            var outText = answer?.Output ?? answer?.Reasoning ?? "(no answer)";
            logger.LogInformation("AgentChat: completed (rephraseMs={r}, searchMs={s}, answerMs={a})", swRephrase.ElapsedMilliseconds, swSearch.ElapsedMilliseconds, swAnswer.ElapsedMilliseconds);
            return Results.Ok(outText);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            logger.LogWarning("AgentChat: answer canceled after {Ms}ms (caller token). Building partial summary from contexts.", swAnswer.ElapsedMilliseconds);
            // Fallback: produce concise partial summary from available contexts so UI can show something useful
            if (contexts is not null && contexts.Any())
            {
                var parts = contexts.Take(6).Select((c, i) => $"[{i + 1}] {Truncate(c.Source, 100)}: {Truncate(c.Content?.Replace('\n', ' ') ?? string.Empty, 240)}");
                var partial = "Partial results (answer timed out):\n" + string.Join("\n\n", parts);
                return Results.Ok(partial);
            }
            return Results.Ok("Answer generation timed out and no contexts were available.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AgentChat: answer failed after {Ms}ms; returning partial search summary.", swAnswer.ElapsedMilliseconds);
            if (contexts is not null && contexts.Any())
            {
                var parts = contexts.Take(6).Select((c, i) => $"[{i + 1}] {Truncate(c.Source, 100)}: {Truncate(c.Content?.Replace('\n', ' ') ?? string.Empty, 240)}");
                var partial = "Partial results (answer failed):\n" + string.Join("\n\n", parts);
                return Results.Ok(partial);
            }
            return Results.Problem("Answer generation failed and no partial results available.");
        }
        finally { swAnswer.Stop(); }

        static string Truncate(string? s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(504);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Agent chat failed");
        return Results.Problem("Agent processing failed");
    }
});

// Document map endpoint
app.MapGet("/api/document-map", async (IConfiguration cfg) =>
{
    var contentRoot = cfg["ContentRoot"] ?? Directory.GetCurrentDirectory();
    var dataDir = Path.Combine(contentRoot, "data");
    var mapPath = Path.Combine(dataDir, "document-map.json");
    if (!File.Exists(mapPath))
        return Results.NotFound(new { error = "document map not found" });
    var json = await File.ReadAllTextAsync(mapPath);
    return Results.Text(json, "application/json");
});

// Indexer endpoints
app.MapPost("/api/search/indexer/run", async (IBackgroundIndexer bgIndexer, CancellationToken ct) =>
{
    var jobId = await bgIndexer.TriggerIndexingAsync(ct);
    return Results.Accepted($"/api/search/indexer/status/{jobId}", new { jobId });
});

app.MapGet("/api/search/indexer/history", async (ISearchIndexerService indexer, int max = 50) =>
{
    var h = await indexer.GetHistoryAsync(max);
    return Results.Ok(h);
});

app.MapGet("/api/search/indexer/status/{jobId:guid}", async (IBackgroundIndexer bgIndexer, Guid jobId) =>
{
    var status = await bgIndexer.GetStatusAsync(jobId);
    if (status is null) return Results.Ok(new { status = "queued" });
    return Results.Ok(status);
});

// Ensure default endpoints and run the application
app.MapDefaultEndpoints();

app.Run();