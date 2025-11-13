using CodeHero.ApiService.Contracts;
using CodeHero.ApiService.Utilities;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeHero.ApiService.Services.Rag;

public sealed class AoaiQuestionRephraser : IQuestionRephraser
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AoaiQuestionRephraser> _log;

    // Detect typical clarification replies
    private static readonly Regex ClarifyPattern = new(@"\b(clarify|clarification|more details|could you|please provide|what do you mean|can you explain|provide more|could you clarify)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly HashSet<string> TrivialInputs = new(StringComparer.OrdinalIgnoreCase)
    {
        "help","hi","hello","bye","thanks","thank you","info","info?"
    };

    public AoaiQuestionRephraser(IHttpClientFactory http, IConfiguration cfg, ILogger<AoaiQuestionRephraser> log)
    {
        _http = http;
        _cfg = cfg;
        _log = log;
    }

    public async Task<string> RephraseAsync(ChatRequest request, CancellationToken ct = default)
    {
        var followRaw = (request.ChatInput ?? string.Empty).Trim();

        // SHORT-CIRCUIT: if the input is trivial (single word or short), return deterministic grounded rephrase
        if (IsTrivialInput(followRaw))
        {
            var det = BuildDeterministicGrounded(request);
            _log.LogInformation("AoaiQuestionRephraser: short/trivial input detected, returning deterministic grounded rephrase='{Rephrase}'", det);
            return det;
        }

        var system = """
        You are a concise rephraser. Given the conversation history and the user's follow-up, produce a single, self-contained question that preserves the user's intent.
        Always ground the standalone question in the application's repository: use the codebase, docs (docs/*), and architecture diagrams as the context. If the user's follow-up is generic, reframe it specifically for this application (for example: 'For this codebase, how would you ...').
        NEVER ask the user for clarification. If the model would ask a clarification question, instead RETURN the best-effort standalone question using the available conversation history and the follow-up input. If insufficient context exists, return the original follow-up input.
        Output only the standalone question (no commentary, no prefixes like 'Question:' or 'Follow-up:'). Keep it brief and do not add extra details beyond what's necessary to make the question standalone.
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

        HttpClient? client = null;
        bool disposeClient = false;
        try
        {
            // Prefer factory-created client (so tests can inject a stubbed client). If factory client unavailable, fall back to direct SocketsHttpHandler.
            try
            {
                client = _http?.CreateClient("foundry");
                if (client is not null)
                {
                    try { client.Timeout = Timeout.InfiniteTimeSpan; } catch { }
                    client.DefaultRequestHeaders.Remove("api-key");
                    client.DefaultRequestHeaders.Add("api-key", key);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                }
            }
            catch (Exception ex)
            {
                _log?.LogDebug(ex, "AoaiQuestionRephraser: IHttpClientFactory CreateClient failed; will create local handler client.");
                client = null;
            }

            if (client is null)
            {
                var handler = new SocketsHttpHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                    Expect100ContinueTimeout = TimeSpan.Zero,
                    UseProxy = false
                };
                client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
                client.DefaultRequestVersion = HttpVersion.Version11;
                client.DefaultRequestHeaders.Remove("api-key");
                client.DefaultRequestHeaders.Add("api-key", key);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                disposeClient = true;
            }

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            req.Headers.ConnectionClose = true;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            sw.Stop();
            _log.LogInformation("AoaiQuestionRephraser sending request id={Id} url={Url} status={Status} elapsedMs={Ms}", Guid.NewGuid().ToString("D"), url, resp.StatusCode, sw.ElapsedMilliseconds);

            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Rephrase call failed status={Status} bodySnippet={Snippet}", (int)resp.StatusCode, body.Length > 300 ? body.Substring(0, 300) : body);
                return request.ChatInput;
            }

            // Use centralized parser
            var parsed = OpenAiResponseParser.TryGetFirstChoiceContent(body);
            var choiceText = parsed ?? (body ?? string.Empty).Trim();

            // If model asked for clarification, override with best-effort deterministic fallback
            if (string.IsNullOrWhiteSpace(choiceText) || ClarifyPattern.IsMatch(choiceText))
            {
                _log.LogInformation("AoaiQuestionRephraser returned clarification-like text; applying deterministic fallback. RawChoiceLength={Len}", choiceText?.Length ?? 0);
                var fallback = BuildBestEffortStandalone(request);
                _log.LogInformation("AoaiQuestionRephraser fallback choice='{Choice}'", fallback);
                return string.IsNullOrWhiteSpace(fallback) ? request.ChatInput : fallback;
            }

            var trimmed = choiceText.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? request.ChatInput : trimmed;
        }
        catch (TaskCanceledException tex)
        {
            _log.LogWarning(tex, "Rephrase call canceled; returning original input.");
            return request.ChatInput;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Rephrase call threw; returning original input.");
            return request.ChatInput;
        }
        finally
        {
            if (disposeClient && client is not null)
                client.Dispose();
        }
    }

    private static bool IsTrivialInput(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return true;
        if (TrivialInputs.Contains(s.Trim())) return true;
        // treat single-word short inputs as trivial
        if (s.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 1 && s.Length <= 6) return true;
        return false;
    }

    private static string BuildDeterministicGrounded(ChatRequest request)
    {
        // Produce a grounded, deterministic question that references repository context
        var follow = (request.ChatInput ?? string.Empty).Trim();
        var lastUser = request.ChatHistory?.Where(h => !string.IsNullOrWhiteSpace(h.User)).Select(h => h.User.Trim()).LastOrDefault();

        // If follow is trivial like 'help', prefer a clear grounded question for the repo
        if (string.Equals(follow, "help", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(follow))
        {
            return "For this repository (CodeHero), using the codebase, docs (docs/*), and architecture diagrams, how do I use the application?";
        }

        // Otherwise combine last user and follow-up but explicitly ground it in the repo
        var subject = !string.IsNullOrWhiteSpace(lastUser) && !string.Equals(lastUser, follow, StringComparison.OrdinalIgnoreCase)
            ? (lastUser + " " + follow).Trim()
            : follow;

        if (!subject.EndsWith("?")) subject += "?";

        return "For this repository (CodeHero), using the codebase, docs (docs/*), and architecture diagrams, " + subject;
    }

    private static string BuildBestEffortStandalone(ChatRequest request)
    {
        // Previous heuristic: combine last user and follow-up
        try
        {
            var lastUser = request.ChatHistory?.Where(h => !string.IsNullOrWhiteSpace(h.User)).Select(h => h.User.Trim()).LastOrDefault();
            var follow = (request.ChatInput ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(lastUser) && !string.Equals(lastUser, follow, StringComparison.OrdinalIgnoreCase))
            {
                var combined = (lastUser + " " + follow).Trim();
                if (!combined.EndsWith("?")) combined += "?";
                return combined;
            }

            if (!string.IsNullOrWhiteSpace(follow))
            {
                return follow.EndsWith("?") ? follow : (follow + "?");
            }

            return string.Empty;
        }
        catch
        {
            return request.ChatInput ?? string.Empty;
        }
    }
}