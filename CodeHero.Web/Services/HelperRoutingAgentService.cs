using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

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
   - POST to Azure Foundry/OpenAI deployments route: {Endpoint}/openai/deployments/{Deployment}/chat/completions?api-version={ApiVersion}.
     Headers: api-key (from config key AzureAI:Foundry:Key or AzureAI:Foundry:ApiKey), Accept: application/json.
   - Body: { "messages": [ {role: "system", content: systemPrompt}, {role: "user", content: userPayload} ] }
   - Parse response first choice content. Return it, append "Confidence: ..." line.
   - On any exception, fall back to deterministic citation rendering (existing code path).
4. Inject IHttpClientFactory into constructor (add private readonly _http).
5. Remove hard-coded summary block and extraneous duplicated sb code at bottom; fix stray code after LooksLikeWhatDoesAppDo.
6. Minor cleanup: avoid adding duplicate candidates (remove second candidates.Add with PathCombine); keep PathCombine helper for consistent forward slashes.
7. Keep existing scoring/snippet extraction.
*/

public class HelperRoutingAgentService : IAgentService
{
    private readonly LlmOrchestratorAgentService _orchestrator;
    private readonly IMcpClient _mcp;
    private readonly IConfiguration _cfg;
    private readonly IHttpClientFactory _http;

    private static readonly Regex ScribeKeywords = new("\\b(create|add|new requirement|draft requirement|scribe)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReqNouns = new("\\b(req|requirement|requirements|REQ-)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ListVerbs = new("\\b(list|show|display)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReadVerb = new("\\bread\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ExplainHints = new("\\b(what|explain|describe|overview|summary)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public HelperRoutingAgentService(
        LlmOrchestratorAgentService orchestrator,
        IMcpClient mcp,
        IConfiguration cfg,
        IHttpClientFactory http)
    {
        _orchestrator = orchestrator;
        _mcp = mcp;
        _cfg = cfg;
        _http = http;
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
            // fallback proceeds to generic deterministic formatting below
        }

        if (top.Length == 0)
            return "I couldn't find relevant passages in the indexed docs. I can try widening the search or you can ask a more specific question.";

        // Deterministic citation-based answer (fallback)
        var sb = new StringBuilder();
        sb.AppendLine("Found relevant passages. See citations below for details.");
        sb.AppendLine();
        sb.AppendLine("Citations:");
        foreach (var t in top)
        {
            var shortSnippet = t.Snippet.Replace('\r', ' ').Replace('\n', ' ');
            if (shortSnippet.Length > 240) shortSnippet = shortSnippet[..240] + "...";
            sb.AppendLine($"- {t.Path}: \"{shortSnippet}\"");
        }
        sb.AppendLine();
        sb.AppendLine($"Confidence: {confidence}");
        sb.AppendLine();
        sb.AppendLine("Follow-ups:");
        sb.AppendLine("- Would you like a requirement drafted from these findings?");
        sb.AppendLine("- Should I search code files as well?");
        return sb.ToString();
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
            var deployment = _cfg["AzureAI:Foundry:Deployment"] ?? _cfg["AzureAI:Foundry:PhiDeployment"] ?? "phi-4";
            var apiVersion = _cfg["AzureAI:Foundry:ApiVersion"] ?? "2024-05-01-preview";

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
                return string.Empty; // signal fallback

            var http = _http.CreateClient();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            http.DefaultRequestHeaders.Add("api-key", key);

            var systemPrompt =
@"You are the CodeHero summarization agent (Phi-4).
Task:
- Answer the user's question using ONLY provided source passages.
- Produce a concise, conversational, technically accurate summary.
- Include markdown bullet points.
- Add inline links to sources: use relative paths as [label](./<path>).
- If describing overall app, group sections (Architecture, Agents, Indexing, Speech, Governance).
- Avoid hallucination: if unsure, state limitations.
Output:
1. Brief intro paragraph (1-2 sentences).
2. Bulleted breakdown.
3. Sources (as a list with links).
4. Follow-up suggestions.
";

            var srcObjects = sources.Select(s => new
            {
                path = s.Path.Replace('\\', '/'),
                score = s.Score,
                snippet = s.Snippet
            });

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
                temperature = 0.3
            }), Encoding.UTF8, "application/json");

            var url = $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";

            // Use a short request timeout to avoid long hangs that cause UI timeouts
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(8));
            try
            {
                using var resp = await http.PostAsync(url, content, linkedCts.Token);
                var body = await resp.Content.ReadAsStringAsync(linkedCts.Token);
                if (!resp.IsSuccessStatusCode)
                    return string.Empty;

                using var doc = JsonDocument.Parse(body);
                var first = doc.RootElement.GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                if (string.IsNullOrWhiteSpace(first))
                    return string.Empty;

                // Append confidence & light guidance
                var sb = new StringBuilder();
                sb.AppendLine(first.Trim());
                sb.AppendLine();
                sb.AppendLine($"Confidence: {confidence}");
                sb.AppendLine("Ask for more detail or request a new requirement if needed.");
                return sb.ToString();
            }
            catch (TaskCanceledException)
            {
                // timed out - return empty to fall back to deterministic answer
                return string.Empty;
            }
        }
        catch
        {
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
}