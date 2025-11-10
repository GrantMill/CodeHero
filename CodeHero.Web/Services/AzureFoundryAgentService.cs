using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CodeHero.Web.Services;

public sealed class AzureFoundryAgentService : IAgentService
{
    private readonly string _endpoint;
    private readonly string _key;
    private readonly string _apiVersion;
    private readonly string _deployment;
    private readonly IHttpClientFactory _http;

    public AzureFoundryAgentService(IConfiguration config, IHttpClientFactory http)
    {
        _endpoint = (config["AzureAI:Foundry:Endpoint"] ?? string.Empty).TrimEnd('/');
        _key = config["AzureAI:Foundry:Key"] ?? string.Empty;
        // Strict: prefer explicit Phi deployment config; fall back to a safe default 'Phi-4'
        _deployment = config["AzureAI:Foundry:PhiDeployment"] ?? "Phi-4";
        _apiVersion = config["AzureAI:Foundry:ApiVersion"] ?? "2024-08-01-preview";
        _http = http;
    }

    public async Task<string> ChatAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_endpoint) || string.IsNullOrWhiteSpace(_key) || string.IsNullOrWhiteSpace(_deployment))
        {
            return "Azure Foundry Agent not configured.";
        }

        try
        {
            // Use the named 'foundry' client so Program.cs tuning applies (HTTP/1.1, handler, infinite timeout)
            var client = _http.CreateClient("foundry");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Remove("api-key");
            client.DefaultRequestHeaders.Add("api-key", _key);

            var url = $"{_endpoint}/openai/deployments/{_deployment}/chat/completions?api-version={_apiVersion}";

            var payload = new
            {
                messages = new[]
                {
                    new { role = "user", content = input }
                },
                temperature = 0.2
            };

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            // cap via linked CTS but rely on client infinite timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(60));

            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            req.Headers.ConnectionClose = true;

            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            var body = await resp.Content.ReadAsStringAsync(cts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                return $"[foundry/error] {(int)resp.StatusCode} {resp.ReasonPhrase}: {body}";
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentEl))
                {
                    return contentEl.GetString() ?? string.Empty;
                }
            }

            return body;
        }
        catch (TaskCanceledException)
        {
            return "[foundry/error] request timed out";
        }
        catch (Exception ex)
        {
            return "[foundry/error] " + ex.Message;
        }
    }
}