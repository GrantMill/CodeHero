using Azure;
using Azure.Search.Documents;
using CodeHero.ApiService.Contracts;
using CodeHero.ApiService.Services.Rag;
using CodeHero.Extensions;
using CodeHero.Services;

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

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}