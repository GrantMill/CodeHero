using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using CodeHero.ApiService.Contracts;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CodeHero.ApiService.Services.Rag;

public sealed class HybridSearchService : IHybridSearchService
{
    private readonly SearchClient _searchClient;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<HybridSearchService> _log;
    private readonly string _vectorField = "contentVector";
    private readonly string _textField = "content";
    private readonly string _sourceMeta = "metadata";

    public HybridSearchService(SearchClient searchClient, IHttpClientFactory http, IConfiguration cfg, ILogger<HybridSearchService> log)
    {
        _searchClient = searchClient;
        _http = http;
        _cfg = cfg;
        _log = log;
    }

    public async Task<SearchResponse> SearchAsync(SearchRequest req, CancellationToken ct = default)
    {
        var vector = await EmbedAsync(req.StandaloneQuestion, ct) ?? Array.Empty<float>();

        var options = new SearchOptions
        {
            Size = req.TopK,
            QueryType = SearchQueryType.Simple // fallback to keyword + vector; remove semantic specifics for SDK compatibility
        };
        options.VectorSearch = new()
        {
            Queries = { new VectorizedQuery(vector) { KNearestNeighborsCount = req.TopK, Fields = { _vectorField } } }
        };
        options.Select.Add(_textField);
        options.Select.Add(_sourceMeta);

        var response = await _searchClient.SearchAsync<SearchDocument>(req.StandaloneQuestion, options, ct);
        var hits = new List<SearchHit>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            var doc = result.Document;
            string content = doc.GetString(_textField) ?? string.Empty;
            string source = ExtractSource(doc);
            hits.Add(new SearchHit(content, source, result.Score ?? 0));
        }
        return new SearchResponse(hits);
    }

    private async Task<float[]?> EmbedAsync(string text, CancellationToken ct)
    {
        var endpoint = _cfg["AOAI:Endpoint"] ?? _cfg["AzureAI:Foundry:Endpoint"] ?? string.Empty;
        var key = _cfg["AOAI:Key"] ?? _cfg["AzureAI:Foundry:Key"] ?? _cfg["AzureAI:Foundry:ApiKey"] ?? string.Empty;
        var deployment = _cfg["AOAI:EmbeddingDeployment"] ?? _cfg["AzureAI:Foundry:EmbeddingDeployment"] ?? _cfg["AzureAI:Foundry:EmbeddingModel"] ?? string.Empty;
        var apiVersion = _cfg["AOAI:ApiVersion"] ?? _cfg["AzureAI:Foundry:ApiVersion"] ?? "2024-08-01-preview";
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(deployment))
            return null;

        var url = endpoint.TrimEnd('/') + $"/openai/deployments/{deployment}/embeddings?api-version={apiVersion}";
        var client = _http.CreateClient("foundry");
        client.DefaultRequestHeaders.Remove("api-key");
        client.DefaultRequestHeaders.Add("api-key", key);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var payload = new { input = text };
        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var resp = await client.PostAsync(url, content, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Embedding failed status={Status} snippet={Snippet}", (int)resp.StatusCode, body.Length > 200 ? body.Substring(0, 200) : body);
                return null;
            }
            using var doc = JsonDocument.Parse(body);
            var arr = doc.RootElement.GetProperty("data")[0].GetProperty("embedding").EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
            return arr;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Embedding exception");
            return null;
        }
    }

    private string ExtractSource(SearchDocument doc)
    {
        if (!doc.TryGetValue(_sourceMeta, out object? metaObj) || metaObj is not IDictionary<string, object> md)
            return string.Empty;
        if (!md.TryGetValue("source", out var srcObj) || srcObj is not IDictionary<string, object> src)
            return string.Empty;
        if (!src.TryGetValue("url", out var urlObj))
            return string.Empty;
        return urlObj?.ToString() ?? string.Empty;
    }
}