using CodeHero.Indexer.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

namespace CodeHero.Indexer.Clients;

public class FoundryEmbeddingClient : IEmbeddingClient
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _endpoint;
    private readonly string _key;

    public FoundryEmbeddingClient(IConfiguration config, IHttpClientFactory http)
    {
        _config = config;
        _httpFactory = http;
        _endpoint = config["Foundry:Endpoint"] ?? string.Empty;
        _key = config["Foundry:Key"] ?? string.Empty;

        if (string.IsNullOrEmpty(_endpoint) || string.IsNullOrEmpty(_key))
        {
            // We'll throw on first use; allow DI to construct the type
        }
    }

    public async System.Threading.Tasks.Task<float[]> EmbedAsync(string text)
    {
        if (string.IsNullOrEmpty(_endpoint) || string.IsNullOrEmpty(_key))
            throw new InvalidOperationException("Foundry endpoint/key not configured");

        // Simple retry policy
        int maxRetries = 3;
        int attempt = 0;
        var backoff = TimeSpan.FromMilliseconds(200);

        while (true)
        {
            attempt++;
            try
            {
                using var client = _httpFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.ExpectContinue = true;
                if (!string.IsNullOrEmpty(_key))
                {
                    // Default to Authorization: Bearer <key>. Adjust if your Foundry expects a different header.
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _key);
                }

                // Build request payload. Many embedding endpoints accept { "input": "..." } or { "input": ["..."] }.
                var payload = new { input = text };
                var body = JsonSerializer.Serialize(payload);
                using var content = new StringContent(body, Encoding.UTF8, "application/json");

                using var resp = await client.PostAsync(_endpoint, content);
                var respText = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[FoundryEmbeddingClient] HTTP {(int)resp.StatusCode} response: {respText}");
                    if (attempt > maxRetries) break;
                    await Task.Delay(backoff);
                    backoff = backoff * 2;
                    continue;
                }

                // Try parsing common response shapes
                try
                {
                    using var doc = JsonDocument.Parse(respText);
                    var root = doc.RootElement;

                    // shape: { "data": [ { "embedding": [..] } ] }
                    if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
                    {
                        var first = data[0];
                        if (first.TryGetProperty("embedding", out var emb) && emb.ValueKind == JsonValueKind.Array)
                        {
                            return emb.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
                        }
                    }

                    // shape: { "embedding": [..] }
                    if (root.TryGetProperty("embedding", out var embRoot) && embRoot.ValueKind == JsonValueKind.Array)
                    {
                        return embRoot.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
                    }

                    // shape: { "outputs": [ { "data": { "embedding": [..] } } ] }
                    if (root.TryGetProperty("outputs", out var outputs) && outputs.ValueKind == JsonValueKind.Array && outputs.GetArrayLength() > 0)
                    {
                        var out0 = outputs[0];
                        if (out0.TryGetProperty("data", out var outData))
                        {
                            if (outData.ValueKind == JsonValueKind.Object && outData.TryGetProperty("embedding", out var emb2) && emb2.ValueKind == JsonValueKind.Array)
                            {
                                return emb2.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
                            }

                            if (outData.ValueKind == JsonValueKind.Array && outData.GetArrayLength() > 0)
                            {
                                var maybe = outData[0];
                                if (maybe.TryGetProperty("embedding", out var emb3) && emb3.ValueKind == JsonValueKind.Array)
                                {
                                    return emb3.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
                                }
                            }
                        }
                    }

                    // Unable to parse embedding
                    Console.WriteLine("[FoundryEmbeddingClient] Unable to parse embedding from response; falling back to mock.");
                    return await new MockEmbeddingClient().EmbedAsync(text);
                }
                catch (JsonException je)
                {
                    Console.WriteLine($"[FoundryEmbeddingClient] JSON parse error: {je.Message}");
                    return await new MockEmbeddingClient().EmbedAsync(text);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FoundryEmbeddingClient] request failed (attempt {attempt}): {ex.Message}");
                if (attempt > maxRetries)
                {
                    Console.WriteLine("[FoundryEmbeddingClient] Max retries exceeded; falling back to mock embedding.");
                    return await new MockEmbeddingClient().EmbedAsync(text);
                }
                await Task.Delay(backoff);
                backoff = backoff * 2;
            }
        }

        // fallback
        return await new MockEmbeddingClient().EmbedAsync(text);
    }
}