using CodeHero.ApiService.Contracts;
using CodeHero.ApiService.Utilities;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CodeHero.ApiService.Services.Rag;

public sealed class RagAnswerService : IRagAnswerService
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<RagAnswerService> _log;

    public RagAnswerService(IHttpClientFactory http, IConfiguration cfg, ILogger<RagAnswerService> log)
    {
        _http = http;
        _cfg = cfg;
        _log = log;
    }

    public async Task<AnswerResponse> AnswerAsync(AnswerRequest req, CancellationToken ct = default)
    {
        var contexts = PromptContextBuilder.Build(req.Contexts);

        var system = """
        You are the CodeHero Helper (RAG summarization agent).

        Task:
        - Use ONLY the provided source passages to answer the user's question. Do not invent facts or rely on external knowledge.
        - When analyzing content:
          1. Group related requirements, features, or design elements together.
          2. Infer what the application does based on repeated entities, filenames, and developer intentions.
          3. State the overall purpose clearly, e.g.: "The app supports conversational navigation, requirement generation, and task execution via orchestrated agents."
          4. Add citations in the form "(Source: <path>)" after each factual statement when possible.
          5. After the natural-language explanation, provide a short table summarizing each cited source with its title and purpose.

        Output rules:
        - Summarize patterns instead of quoting raw text unless absolutely necessary.
        - Use relative paths for citations (e.g., docs/requirements/REQ-001.md).
        - Keep the summary concise, human-readable, and suitable for a non-technical audience.
        - If uncertain, set confidence to "medium" or "low" and mention the uncertainty in the prose summary.

        Context:
        """ + contexts;

        var history = string.Join("\n", req.ChatHistory.Select(h =>
            $"user:\n{h.User}\nassistant:\n{h.Assistant}"));

        var user = $$"""
        Chat history:
        {history}

        User:
        {req.ChatInput}
        """;

        var endpoint = _cfg["AOAI:Endpoint"] ?? _cfg["AzureAI:Foundry:Endpoint"] ?? string.Empty;
        var key = _cfg["AOAI:Key"] ?? _cfg["AzureAI:Foundry:Key"] ?? _cfg["AzureAI:Foundry:ApiKey"] ?? string.Empty;
        var deployment = _cfg["AOAI:ChatDeployment"] ?? _cfg["AzureAI:Foundry:PhiDeployment"] ?? _cfg["AzureAI:Foundry:ChatDeployment"] ?? _cfg["AzureAI:Foundry:Deployment"] ?? _cfg["AzureAI:Foundry:Model"] ?? "Phi-4";
        var apiVersion = _cfg["AOAI:ApiVersion"] ?? _cfg["AzureAI:Foundry:ApiVersion"] ?? "2024-08-01-preview";

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
        {
            _log.LogWarning("Answer missing endpoint/key; returning fallback.");
            return new AnswerResponse("Configuration missing for AOAI/Foundry.", null, "low");
        }

        var url = endpoint.TrimEnd('/') + $"/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";
        var payload = new
        {
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            },
            temperature = 0.2,
            max_tokens = 1000
        };

        // Use a direct SocketsHttpHandler + HttpClient to bypass any global resilience/timeouts applied to the named client.
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            Expect100ContinueTimeout = TimeSpan.Zero,
            UseProxy = false
        };

        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestVersion = HttpVersion.Version11;
        client.DefaultRequestHeaders.Remove("api-key");
        client.DefaultRequestHeaders.Add("api-key", key);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var reqMsg = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            reqMsg.Headers.ConnectionClose = true;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            // Send headers first to measure TTFB
            using var resp = await client.SendAsync(reqMsg, HttpCompletionOption.ResponseHeadersRead, ct);
            var ttfb = sw.ElapsedMilliseconds;

            var body = await resp.Content.ReadAsStringAsync(ct);
            sw.Stop();
            var total = sw.ElapsedMilliseconds;
            _log.LogInformation("Foundry call: URL={Url} Status={Status} TTFB={TTFB}ms Total={Total}ms Version={Ver}", url, (int)resp.StatusCode, ttfb, total, resp.Version);

            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Answer call failed status={Status} bodySnippet={Snippet}", (int)resp.StatusCode, body.Length > 300 ? body.Substring(0, 300) : body);
                return new AnswerResponse("Answer generation failed.", null, "low");
            }

            // Try to parse as JSON; if not JSON, treat the whole body as assistant text
            try
            {
                var choice = OpenAiResponseParser.TryGetFirstChoiceContent(body);
                if (choice is null)
                {
                    var raw = (body ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(raw))
                    {
                        _log.LogWarning("Answer returned empty non-JSON body.");
                        return new AnswerResponse("Answer generation returned empty response.", null, "low");
                    }

                    _log.LogInformation("Answer returned non-JSON or unexpected JSON; returning raw text. Length={Len}", raw.Length);
                    return new AnswerResponse(raw, null, "medium");
                }

                var contentStr = choice ?? string.Empty;
                var confidence = contentStr.Contains("confidence", StringComparison.OrdinalIgnoreCase) ? "reported" : "medium";
                return new AnswerResponse(contentStr.Trim(), null, confidence);
            }
            catch (JsonException)
            {
                var raw = (body ?? string.Empty).Trim();
                _log.LogInformation("Answer returned non-JSON body after parse attempt; returning raw text. Length={Len}", raw.Length);
                return new AnswerResponse(raw, null, "medium");
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Answer call threw");
            return new AnswerResponse("Answer generation error.", null, "low");
        }
    }
}