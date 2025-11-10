using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CodeHero.Web.Services;

/*
Plan (pseudocode):
1. Keep existing routing logic for scribe/list/read intents.
2. After retrieval of candidate passages (top results), instead of hard-coded "what does the application do" block:
   - Always call a new private method SummarizeWithPhiAsync(question, top, confidence) if explanatory (either LooksLikeWhatDoesAppDo or generic explanatory: presence of verbs like "explain", "what", "describe").
3. SummarizeWithPhiAsync:
   - If no sources -> return existing fallback.
   - Build a source payload (JSON) with path, snippet, score.
   - Construct system prompt instructing Phi-4 to:
        * Use only provided sources.
        * Produce conversational intelligent summary.
        * Add markdown links referencing relative paths.
        * Provide follow-up suggestions.
        * Output JSON matching the Helper schema.
   - POST to Azure Foundry/OpenAI deployments route: {Endpoint}/openai/deployments/{Deployment}/chat/completions?api-version={ApiVersion}.
     Headers: api-key (from config key AzureAI:Foundry:Key or AzureAI:Foundry:ApiKey), Accept: application/json.
   - Body: { "messages": [ {role: "system", content: systemPrompt}, {role: "user", content: userPayload} ] }
   - Parse response first choice content as JSON. Return it. On failure, fall back to deterministic citation rendering (existing code path) but now using JSON schema.
4. Inject IHttpClientFactory into constructor (add private readonly _http).
5. Minor cleanup: avoid adding duplicate candidates (remove second candidates.Add with PathCombine); keep PathCombine helper for consistent forward slashes.
6. Keep existing scoring/snippet extraction.
*/

public class HelperRoutingAgentService : IAgentService
{
    private readonly LlmOrchestratorAgentService _orchestrator;
    private readonly IMcpClient _mcp;
    private readonly IConfiguration _cfg;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<HelperRoutingAgentService> _log;

    private static readonly Regex ScribeKeywords = new("\\b(create|add|new requirement|draft requirement|scribe)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReqNouns = new("\\b(req|requirement|requirements|REQ-)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ListVerbs = new("\\b(list|show|display)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReadVerb = new("\\bread\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ExplainHints = new("\\b(what|explain|describe|overview|summary)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public HelperRoutingAgentService(
        LlmOrchestratorAgentService orchestrator,
        IMcpClient mcp,
        IConfiguration cfg,
        IHttpClientFactory http,
        ILogger<HelperRoutingAgentService> log)
    {
        _orchestrator = orchestrator;
        _mcp = mcp;
        _cfg = cfg;
        _http = http;
        _log = log;
    }

    public async Task<string> ChatAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // Delegate create / list / read intents to orchestrator
        if (ScribeKeywords.IsMatch(text))
            return await _orchestrator.ChatAsync(text, ct);

        var isListReq = ListVerbs.IsMatch(text) && ReqNouns.IsMatch(text);
        var isReadReq = ReadVerb.IsMatch(text) && ReqNouns.IsMatch(text);
        if (isListReq || isReadReq)
            return await _orchestrator.ChatAsync(text, ct);

        // Retrieval over Requirements & Architecture
        var qterms = TokenizeQuery(text).Take(10).ToArray();
        var candidates = new List<(string Path, string Snippet, int Score)>();

        // Requirements
        try
        {
            var reqFiles = await _mcp.ListAsync(StoreRoot.Requirements, new[] { ".md" }, ct);
            foreach (var f in reqFiles)
            {
                try
                {
                    var body = await _mcp.ReadTextAsync(StoreRoot.Requirements, f, new[] { ".md" }, ct);
                    var score = ScoreText(body, qterms);
                    if (score > 0)
                    {
                        var snip = ExtractSnippet(body, qterms);
                        candidates.Add((PathCombine("docs/requirements", f), snip, score));
                    }
                }
                catch { /* ignore per-file errors */ }
            }
        }
        catch { /* ignore listing errors */ }

        // Architecture
        try
        {
            var archFiles = await _mcp.ListAsync(StoreRoot.Architecture, new[] { ".md", ".mmd" }, ct);
            foreach (var f in archFiles)
            {
                try
                {
                    var body = await _mcp.ReadTextAsync(StoreRoot.Architecture, f, new[] { ".md", ".mmd" }, ct);
                    var score = ScoreText(body, qterms);
                    if (score > 0)
                    {
                        var snip = ExtractSnippet(body, qterms);
                        candidates.Add((PathCombine("docs/architecture", f), snip, score));
                    }
                }
                catch { }
            }
        }
        catch { }

        var top = candidates
            .OrderByDescending(c => c.Score)
            .Take(6)
            .ToArray();

        double avgScore = top.Length == 0 ? 0 : top.Average(t => t.Score);
        var confidence = avgScore >= 3 ? "high" : (avgScore >= 1 ? "medium" : "low");

        // Decide if this is explanatory/summarization intent
        var needsSummarization = LooksLikeWhatDoesAppDo(text) || ExplainHints.IsMatch(text);

        if (needsSummarization && top.Length > 0)
        {
            // Use Phi-4 (Foundry deployment) to synthesize answer
            var phiAnswer = await SummarizeWithPhiAsync(text, top, confidence, ct);
            if (!string.IsNullOrWhiteSpace(phiAnswer))
                return phiAnswer;
            // fallback proceeds to deterministic JSON formatting below
        }

        if (top.Length == 0)
            return JsonSerializer.Serialize(new
            {
                summary = "I couldn't find relevant passages in the indexed docs. I can try widening the search or you can ask a more specific question.",
                citations_summary = Array.Empty<object>(),
                citations = Array.Empty<object>(),
                confidence = confidence,
                followups = new[] { "Would you like me to widen the search?", "Should I search implementation files as well?" }
            }, new JsonSerializerOptions { WriteIndented = true });

        // Deterministic citation-based JSON answer (fallback)
        var citations = top.Select(t => new { source = t.Path, snippet = t.Snippet }).ToArray();

        // Create a simple citations_summary by inferring a short topic from filename and snippet
        var citationsSummary = top.Select(t => new
        {
            source = t.Path,
            topic = InferTopicFromSnippet(t.Path, t.Snippet),
            role = InferRoleFromPath(t.Path)
        }).ToArray();

        var deterministicSummary = "Found relevant passages. See citations below for supporting evidence.\n\nSynthesis: " + String.Join(" ", top.Select(t => Truncate(t.Snippet.Replace('\r', ' ').Replace('\n', ' '), 160)).Take(3));

        var fallbackObj = new
        {
            summary = deterministicSummary,
            citations_summary = citationsSummary,
            citations = citations,
            confidence = confidence,
            followups = new[] { "Would you like a requirement drafted from these findings?", "Should I search code files as well?" }
        };

        return JsonSerializer.Serialize(fallbackObj, new JsonSerializerOptions { WriteIndented = true });
    }

    private static bool LooksLikeWhatDoesAppDo(string text)
    {
        var s = text.ToLowerInvariant();
        return (s.Contains("what does") && (s.Contains("application") || s.Contains("codehero") || s.Contains("app")))
               || s.StartsWith("what is codehero")
               || s.StartsWith("what does codehero do");
    }

    private async Task<string> SummarizeWithPhiAsync(string question, (string Path, string Snippet, int Score)[] sources, string confidence, CancellationToken ct)
    {
        try
        {
            var endpoint = _cfg["AzureAI:Foundry:Endpoint"];
            var key = _cfg["AzureAI:Foundry:Key"] ?? _cfg["AzureAI:Foundry:ApiKey"];
            // Prefer explicit Phi deployment config; fall back to other deployment/model keys. Default to 'Phi-4' (capitalized) which matches the service deployment name.
            var deployment = _cfg["AzureAI:Foundry:PhiDeployment"]
                ?? _cfg["AzureAI:Foundry:ChatDeployment"]
                ?? _cfg["AzureAI:Foundry:Deployment"]
                ?? _cfg["AzureAI:Foundry:Model"]
                ?? "Phi-4";
            var apiVersion = _cfg["AzureAI:Foundry:ApiVersion"] ?? "2024-08-01-preview";

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
            {
                _log?.LogWarning("Foundry not configured: Endpoint or Key missing.");
                return string.Empty; // signal fallback
            }

            _log?.LogInformation("SummarizeWithPhiAsync using deployment '{Deployment}' at endpoint {Endpoint}", deployment, endpoint);

            var http = _http.CreateClient("foundry");
            try { http.Timeout = Timeout.InfiniteTimeSpan; } catch { }
            http.DefaultRequestHeaders.Remove("api-key");
            http.DefaultRequestHeaders.Add("api-key", key);
            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var systemPrompt = @"You are the CodeHero Helper (RAG summarization agent).
Task:
- Use ONLY the provided source passages to answer the user's question. Do not invent facts.
- First, synthesize a concise, human-readable summary that:
  1) Groups related requirements or concepts together.
  2) Infers what the application does based on recurring patterns, entity names, and user intents.
  3) States the high-level purpose (e.g., ''The app enables conversational navigation, requirement generation, and task execution via orchestrated agents.'').
  4) Uses the citations only to ground statements (do not quote verbatim).
  5) End with a short table summarizing each cited requirement with its title and purpose.
- After the natural-language summary, provide a structured JSON object matching the Helper schema below. The JSON MUST be the only JSON output (no extra prose inside the JSON block):

{ ""summary"": ""string"",
  ""citations_summary"": [ { ""source"": ""path"", ""topic"": ""string"", ""role"": ""string"" } ],
  ""citations"": [ { ""source"": ""path"", ""snippet"": ""raw excerpt"" } ],
  ""confidence"": ""high | medium | low"",
  ""followups"": [ ""optional"" ]
}

Output rules:
- Prefer to explain patterns over quoting text. Treat multiple similar snippets as corroborating evidence.
- Make the natural-language summary suitable for a non-technical reader before any file paths are listed.
- Use relative paths in citations (e.g., docs/requirements/REQ-001.md).
- Keep the JSON compact and valid. If unsure about details, set confidence to ""medium"" or ""low"" and state limitations in the natural-language summary section (outside the JSON).
";

            var srcObjects = sources.Select(s => new
            {
                source = s.Path.Replace('\\', '/'),
                score = s.Score,
                snippet = s.Snippet
            }).ToArray();

            var userPayload = new
            {
                question,
                confidence,
                sources = srcObjects
            };

            using var content = new StringContent(JsonSerializer.Serialize(new
            {
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = JsonSerializer.Serialize(userPayload) }
                },
                temperature = 0.2
            }), Encoding.UTF8, "application/json");

            var url = $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";

            _log?.LogInformation("Prepared Foundry request to {Url} with {SourceCount} sources (confidence={Confidence})", url, sources.Length, confidence);

            // Let linked CTS cap total runtime (~60s for sanity check)
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(60));

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = content
                };

                // ensure server closes connection after response to avoid HTTP/2 keepalive behaviors
                req.Headers.ConnectionClose = true;

                var t0 = Stopwatch.StartNew();

                // Send and read headers early to measure TTFB
                using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
                var ttfb = t0.ElapsedMilliseconds;

                _log?.LogInformation("Foundry response headers received. TTFB={TTFB}ms Version={Version} Status={Status}", ttfb, resp.Version, resp.StatusCode);

                if (!resp.IsSuccessStatusCode)
                {
                    var respBodyErr = string.Empty;
                    try { respBodyErr = await resp.Content.ReadAsStringAsync(linkedCts.Token); } catch { }
                    _log?.LogWarning("Foundry returned non-success {Status} body: {Body}", (int)resp.StatusCode, Truncate(respBodyErr ?? string.Empty, 2000));
                    return string.Empty;
                }

                // Read entire response body now and log total time
                var respBodySuccess = await resp.Content.ReadAsStringAsync(linkedCts.Token);
                var total = t0.ElapsedMilliseconds;
                _log?.LogInformation("Foundry total response time: {TotalMs}ms (TTFB={TTFB}ms)", total, ttfb);

                // Stream and parse JSON
                try
                {
                    await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(respBodySuccess ?? string.Empty));
                    using var root = await JsonDocument.ParseAsync(stream, cancellationToken: linkedCts.Token);

                    var first = root.RootElement.GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    if (string.IsNullOrWhiteSpace(first))
                    {
                        _log?.LogWarning("Foundry returned empty choice content. Response body: {Body}", Truncate(respBodySuccess ?? string.Empty, 2000));
                        return string.Empty;
                    }

                    // validate returned JSON block
                    try
                    {
                        using var parsed = JsonDocument.Parse(first);
                        _log?.LogInformation("Foundry returned valid JSON content (length={Len})", first.Length);
                        return first;
                    }
                    catch (Exception pex)
                    {
                        _log?.LogError(pex, "Returned content was not valid JSON. Content: {Content}", Truncate(first, 2000));
                        return string.Empty;
                    }
                }
                catch (JsonException jex)
                {
                    _log?.LogError(jex, "Failed to parse Foundry JSON response. Body: {Body}", Truncate(respBodySuccess ?? string.Empty, 2000));
                    return string.Empty;
                }
            }
            catch (TaskCanceledException tex)
            {
                var wasLinkedCanceled = linkedCts.IsCancellationRequested;
                var userCanceled = ct.IsCancellationRequested;
                _log?.LogWarning(tex, "Foundry request canceled. linkedCanceled={Linked} userCanceled={User}", wasLinkedCanceled, userCanceled);
                return string.Empty;
            }
            catch (HttpRequestException hrex)
            {
                _log?.LogError(hrex, "HTTP request to Foundry failed");
                return string.Empty;
            }
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Unexpected error in SummarizeWithPhiAsync");
            return string.Empty;
        }
    }

    private static string PathCombine(string a, string b)
    {
        var combined = System.IO.Path.Combine(a, b);
        return combined.Replace('\\', '/');
    }

    private static IEnumerable<string> TokenizeQuery(string q)
    {
        var words = Regex.Matches(q.ToLowerInvariant(), "[a-z0-9]{2,}")
            .Select(m => m.Value)
            .Where(w => w.Length > 1)
            .Distinct();
        return words;
    }

    private static int ScoreText(string text, string[] terms)
    {
        if (terms.Length == 0) return 0;
        var score = 0;
        var lower = text.ToLowerInvariant();
        foreach (var t in terms)
        {
            var count = Regex.Matches(lower, Regex.Escape(t)).Count;
            score += Math.Min(count, 5); // cap per term
        }
        return score;
    }

    private static string ExtractSnippet(string text, string[] terms)
    {
        var lower = text.ToLowerInvariant();
        foreach (var t in terms)
        {
            var idx = lower.IndexOf(t);
            if (idx >= 0)
            {
                var start = Math.Max(0, idx - 80);
                var len = Math.Min(240, text.Length - start);
                return text.Substring(start, len).Replace("\r\n", " ").Replace('\n', ' ');
            }
        }
        // fallback first 240 chars
        return text.Length <= 240 ? text : text.Substring(0, 240);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";

    private static string InferTopicFromSnippet(string path, string snippet)
    {
        // Try to infer a short topic from snippet first sentence or filename
        try
        {
            var firstLine = snippet?.Split(new[] { '.', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(firstLine))
                return Truncate(firstLine, 80);
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            return name ?? path;
        }
        catch { return path; }
    }

    private static string InferRoleFromPath(string path)
    {
        // Simple heuristic: requirements files are 'Defines requirement' and architecture files 'Describes architecture'
        if (path.Contains("/requirements/", StringComparison.OrdinalIgnoreCase) || path.Contains("\\requirements\\", StringComparison.OrdinalIgnoreCase))
            return "Defines requirement or acceptance criteria";
        if (path.Contains("/architecture/", StringComparison.OrdinalIgnoreCase) || path.Contains("\\architecture\\", StringComparison.OrdinalIgnoreCase))
            return "Describes architecture or component relationships";
        return "Provides supporting information";
    }
}