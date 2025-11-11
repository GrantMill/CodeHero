using CodeHero.Indexer.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace CodeHero.Indexer.Clients;

public class FoundryEmbeddingClient : IEmbeddingClient
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _endpoint;
    private readonly string _key;
    private readonly int _batchSize;
    private readonly string _model;
    private readonly string _apiVersion;

    public FoundryEmbeddingClient(IConfiguration config, IHttpClientFactory http)
    {
        _config = config;
        _httpFactory = http;
        _endpoint = config["Foundry:Endpoint"] ?? string.Empty;
        _key = config["Foundry:Key"] ?? string.Empty;
        _batchSize = int.TryParse(config["Foundry:BatchSize"], out var b) ? b : 8;
        _model = config["Foundry:Model"] ?? string.Empty;
        _apiVersion = config["Foundry:ApiVersion"] ?? "2023-06-01-preview"; // adjustable
    }

    public async System.Threading.Tasks.Task<float[]> EmbedAsync(string text)
    {
        var arr = await EmbedBatchAsync(new[] { text });
        return arr.Length > 0 ? arr[0] : await new MockEmbeddingClient().EmbedAsync(text);
    }

    public async System.Threading.Tasks.Task<float[][]> EmbedBatchAsync(IEnumerable<string> texts)
    {
        if (string.IsNullOrEmpty(_endpoint) || string.IsNullOrEmpty(_key) || string.IsNullOrEmpty(_model))
            throw new InvalidOperationException("Foundry endpoint/key/model not configured");

        var inputs = texts.ToArray();
        var results = new List<float[]>();

        // compute URL: if endpoint already contains "/openai/" assume it's the full path; otherwise build deployment embeddings path
        string baseUrl = _endpoint.TrimEnd('/');
        string embeddingsUrl;
        if (baseUrl.Contains("/openai/", StringComparison.OrdinalIgnoreCase))
        {
            embeddingsUrl = baseUrl; // assume user provided full URL
        }
        else
        {
            // Azure OpenAI style: https://{endpoint}/openai/deployments/{deployment}/embeddings?api-version={version}
            embeddingsUrl = $"{baseUrl}/openai/deployments/{_model}/embeddings?api-version={_apiVersion}";
        }

        // send in batches
        for (int start = 0; start < inputs.Length; start += _batchSize)
        {
            var batch = inputs.Skip(start).Take(_batchSize).ToArray();
            var payload = new Dictionary<string, object>
            {
                ["input"] = batch,
            };
            var body = JsonSerializer.Serialize(payload);

            using var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(120);
            client.DefaultRequestHeaders.ExpectContinue = true;
            // Azure OpenAI / Foundry expects 'api-key' header for Azure-hosted endpoints
            client.DefaultRequestHeaders.Remove("api-key");
            client.DefaultRequestHeaders.Add("api-key", _key);

            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var resp = await client.PostAsync(embeddingsUrl, content);
            var respText = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"[FoundryEmbeddingClient] HTTP {(int)resp.StatusCode} response: {respText}");
                // fallback: produce mock for each input
                foreach (var _ in batch) results.Add(await new MockEmbeddingClient().EmbedAsync(""));
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(respText);
                var root = doc.RootElement;

                // expected shapes:
                // Azure OpenAI: { "data": [ { "embedding": [...] }, ... ] }
                // some providers: { "embeddings": [ [...], ... ] }
                if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        if (item.TryGetProperty("embedding", out var emb) && emb.ValueKind == JsonValueKind.Array)
                        {
                            results.Add(emb.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray());
                        }
                        else
                        {
                            results.Add(await new MockEmbeddingClient().EmbedAsync(""));
                        }
                    }
                }
                else if (root.TryGetProperty("embeddings", out var embs) && embs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var emb in embs.EnumerateArray())
                    {
                        if (emb.ValueKind == JsonValueKind.Array)
                            results.Add(emb.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray());
                        else
                            results.Add(await new MockEmbeddingClient().EmbedAsync(""));
                    }
                }
                else
                {
                    Console.WriteLine("[FoundryEmbeddingClient] Unexpected response shape, falling back to mock for this batch.");
                    foreach (var _ in batch) results.Add(await new MockEmbeddingClient().EmbedAsync(""));
                }
            }
            catch (JsonException je)
            {
                Console.WriteLine($"[FoundryEmbeddingClient] JSON parse error: {je.Message}");
                foreach (var _ in batch) results.Add(await new MockEmbeddingClient().EmbedAsync(""));
            }
        }

        return results.ToArray();
    }
}