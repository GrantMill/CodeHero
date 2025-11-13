using CodeHero.ApiService;
using Azure;
using Azure.Search.Documents;
using CodeHero.ApiService.Contracts;
using CodeHero.ApiService.Services.Rag;
using CodeHero.Extensions;
using CodeHero.Services;
using Polly;
using Polly.Extensions.Http;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;

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

builder.Services.AddScoped<IQuestionRephraser, AoaiQuestionRephraser>();
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

// Indexer status endpoint for diagnostics
app.MapGet("/api/search/indexer/diagnostics", (IConfiguration cfg) =>
{
    var endpoint = cfg["Search:Endpoint"] ?? cfg["AzureSearch:Endpoint"];
    var hasEndpoint = !string.IsNullOrWhiteSpace(endpoint);
    var hasApiKey = !string.IsNullOrWhiteSpace(cfg["Search:ApiKey"] ?? cfg["AzureSearch:ApiKey"]);
    var indexName = cfg["Search:IndexName"] ?? cfg["AzureSearch:IndexName"] ?? "(default: codehero-docs)";
    var contentRoot = cfg["ContentRoot"] ?? Directory.GetCurrentDirectory();
    var dataDir = Path.Combine(contentRoot, "data");
    var mapPath = Path.Combine(dataDir, "document-map.json");
    var mapExists = File.Exists(mapPath);

    return Results.Ok(new
    {
        HasEndpoint = hasEndpoint,
        HasApiKey = hasApiKey,
        Endpoint = hasEndpoint ? endpoint : null,
        IndexName = indexName,
        ContentRoot = contentRoot,
        DocumentMapPath = mapPath,
        DocumentMapExists = mapExists
    });
});

app.MapDefaultEndpoints();

app.Run();