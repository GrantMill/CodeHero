using CodeHero.ApiService.Contracts;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CodeHero.ApiService.Services.Rag;

public sealed class AoaiQuestionRephraser : IQuestionRephraser
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AoaiQuestionRephraser> _log;

    public AoaiQuestionRephraser(IHttpClientFactory http, IConfiguration cfg, ILogger<AoaiQuestionRephraser> log)
    {
        _http = http;
        _cfg = cfg;
        _log = log;
    }

    public async Task<string> RephraseAsync(ChatRequest request, CancellationToken ct = default)
    {
        var system = """
        Given the following conversation history and the user's next question, rephrase the question to be a stand-alone question.
        If the conversation is irrelevant or empty, just restate the original question.
        Do not add more details than necessary to the question.
        """;

        var history = string.Join("\n", request.ChatHistory.Select(h =>
            $"user:\n{h.User}\nassistant:\n{h.Assistant}"));

        var user = $$"""
        chat history:
        {history}

        Follow up Input: {request.ChatInput}
        Standalone Question:
        """;

        var endpoint = _cfg["AOAI:Endpoint"] ?? _cfg["AzureAI:Foundry:Endpoint"] ?? string.Empty;
        var key = _cfg["AOAI:Key"] ?? _cfg["AzureAI:Foundry:Key"] ?? _cfg["AzureAI:Foundry:ApiKey"] ?? string.Empty;
        var deployment = _cfg["AOAI:ChatDeployment"] ?? _cfg["AzureAI:Foundry:PhiDeployment"] ?? _cfg["AzureAI:Foundry:ChatDeployment"] ?? _cfg["AzureAI:Foundry:Deployment"] ?? _cfg["AzureAI:Foundry:Model"] ?? "Phi-4";
        var apiVersion = _cfg["AOAI:ApiVersion"] ?? _cfg["AzureAI:Foundry:ApiVersion"] ?? "2024-08-01-preview";

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
        {
            _log.LogWarning("Rephraser missing endpoint/key configuration.");
            return request.ChatInput;
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

        var client = _http.CreateClient("foundry");
        client.DefaultRequestHeaders.Remove("api-key");
        client.DefaultRequestHeaders.Add("api-key", key);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            req.Headers.ConnectionClose = true;
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Rephrase call failed status={Status} bodySnippet={Snippet}", (int)resp.StatusCode, body.Length > 300 ? body.Substring(0, 300) : body);
                return request.ChatInput;
            }
            using var doc = JsonDocument.Parse(body);
            var choice = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return string.IsNullOrWhiteSpace(choice) ? request.ChatInput : choice.Trim();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Rephrase call threw; returning original input.");
            return request.ChatInput;
        }
    }
}