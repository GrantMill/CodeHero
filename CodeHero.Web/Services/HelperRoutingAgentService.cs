using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeHero.Web.Services;

public class HelperRoutingAgentService : IAgentService
{
    private readonly LlmOrchestratorAgentService _orchestrator;
    private readonly IMcpClient _mcp;
    private readonly IConfiguration _cfg;
    private static readonly Regex ScribeKeywords = new("\\b(create|add|new requirement|draft requirement|scribe)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReqNouns = new("\\b(req|requirement|requirements|REQ-)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ListVerbs = new("\\b(list|show|display)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReadVerb = new("\\bread\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public HelperRoutingAgentService(LlmOrchestratorAgentService orchestrator, IMcpClient mcp, IConfiguration cfg)
    {
        _orchestrator = orchestrator;
        _mcp = mcp;
        _cfg = cfg;
    }

    public async Task<string> ChatAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // If the user asks to create a requirement, delegate to orchestrator which handles scribe flows
        if (ScribeKeywords.IsMatch(text))
        {
            return await _orchestrator.ChatAsync(text, ct);
        }

        // If this is a simple list/read request about requirements, let the orchestrator handle it (calls MCP tools directly)
        var isListReq = ListVerbs.IsMatch(text) && ReqNouns.IsMatch(text);
        var isReadReq = ReadVerb.IsMatch(text) && ReqNouns.IsMatch(text);
        if (isListReq || isReadReq)
        {
            return await _orchestrator.ChatAsync(text, ct);
        }

        // Otherwise treat as Helper/RAG intent: perform a simple retrieval over Requirements and Architecture
        var qterms = TokenizeQuery(text).Take(10).ToArray();
        var candidates = new List<(string Path, string Snippet, int Score)>();

        // Search Requirements
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
                        candidates.Add((Path.Combine("docs/requirements", f), snip, score));
                    }
                }
                catch { }
            }
        }
        catch { }

        // Search Architecture
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
                        candidates.Add((Path.Combine("docs/architecture", f), snip, score));
                    }
                }
                catch { }
            }
        }
        catch { }

        // Pick top results
        var top = candidates.OrderByDescending(c => c.Score).Take(6).ToArray();
        double avgScore = top.Length == 0 ? 0 : top.Average(t => t.Score);
        var confidence = avgScore >= 3 ? "high" : (avgScore >= 1 ? "medium" : "low");

        // Human-readable summary
        if (top.Length == 0)
        {
            return "I couldn't find relevant passages in the indexed docs. I can try widening the search or you can ask a more specific question.";
        }

        var sb = new System.Text.StringBuilder();
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
