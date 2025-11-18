using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CodeHero.ApiService.Services.Rag;

internal sealed class FoundryEmbeddingProvider : IEmbeddingProvider
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<FoundryEmbeddingProvider> _log;

    public FoundryEmbeddingProvider(IHttpClientFactory http, IConfiguration cfg, ILogger<FoundryEmbeddingProvider> log)
    {
        _http = http;
        _cfg = cfg;
        _log = log;
    }

    public async Task<float[]?> GetEmbeddingAsync(string input, CancellationToken ct = default)
    {
        var endpoint = _cfg["AzureAI:Foundry:Endpoint"] ?? string.Empty;
        var key = _cfg["AzureAI:Foundry:Key"] ?? string.Empty;
        var deployment = _cfg["AzureAI:Foundry:EmbeddingModel"] ?? string.Empty;
        var apiVersion = _cfg["AzureAI:Foundry:ApiVersion"] ?? "2024-08-01-preview";
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(deployment))
            return null;

        var url = endpoint.TrimEnd('/') + $"/openai/deployments/{deployment}/embeddings?api-version={apiVersion}";
        var client = _http.CreateClient("foundry");
        client.DefaultRequestHeaders.Remove("api-key");
        client.DefaultRequestHeaders.Add("api-key", key);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var payload = new { input };
        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var resp = await client.PostAsync(url, content, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Foundry embedding failed status={Status} snippetLength={Len}", (int)resp.StatusCode, body?.Length ?? 0);
                return null;
            }
            using var doc = JsonDocument.Parse(body);
            var arr = doc.RootElement.GetProperty("data")[0].GetProperty("embedding").EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
            return arr;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Foundry embedding exception");
            return null;
        }
    }
}
