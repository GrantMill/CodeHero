using System.Text.RegularExpressions;

namespace CodeHero.Web.Services;

/// <summary>
/// Rule-based orchestrator that maps user requests to allowed MCP tool calls via IMcpClient.
/// Accepts common phrasing, punctuation, and some misrecognitions from STT.
/// </summary>
public sealed class McpOrchestratorAgentService : IAgentService
{
    // Direct filename read: e.g., "read REQ-000.md" or "read REQ000.md"
    private static readonly Regex ReadReqTerse = new(
    @"^read\s+(?<name>REQ-?\d{1,4}\.md)$",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IMcpClient _mcp;

    public McpOrchestratorAgentService(IMcpClient mcp) => _mcp = mcp;

    public async Task<string> ChatAsync(string text, CancellationToken ct = default)
    {
        var raw = (text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(raw)) return "(empty)";
        var norm = NormalizeText(raw); // lower, remove punctuation, collapse spaces, handle mishears
        var tokens = norm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // LIST intents (accept: list/show/display {req|requirement(s)|wreck(s)})
        if (IsListIntent(tokens))
        {
            var files = await _mcp.ListAsync(StoreRoot.Requirements, new[] { ".md" }, ct);
            return files.Count == 0 ? "No requirements found." : string.Join("\n", files);
        }

        // Direct filename read
        var direct = ReadReqTerse.Match(raw);
        if (direct.Success)
        {
            var name = NormalizeReqFilename(direct.Groups["name"].Value);
            var body = await _mcp.ReadTextAsync(StoreRoot.Requirements, name, new[] { ".md" }, ct);
            return $"--- {name} ---\n{body}";
        }

        // Read intent spoken: accept forms like
        // "read requirement zero zero one", "read rec dashed zero zero zero",
        // "requirement one" ? treat as read, "req12" ? read012
        if (IsReadIntent(tokens))
        {
            var id = ExtractRequirementId(tokens);
            if (id is not null)
            {
                var name = $"REQ-{id}.md";
                var body = await _mcp.ReadTextAsync(StoreRoot.Requirements, name, new[] { ".md" }, ct);
                return $"--- {name} ---\n{body}";
            }
        }

        // Fallback: guidance
        return "Sorry, I can: list req | read REQ-xxx.md | read requirement <zero|one|...> (e.g., 'read requirement zero zero zero') | create REQ-xxx Title";
    }

    private static string NormalizeText(string input)
    {
        // lower + strip punctuation (keep letters, digits, space, dash)
        Span<char> buf = stackalloc char[input.Length];
        int j = 0;
        foreach (var ch in input)
        {
            var c = char.ToLowerInvariant(ch);
            if (char.IsLetterOrDigit(c) || c == ' ' || c == '-')
                buf[j++] = c;
            else
                buf[j++] = ' ';
        }
        var s = new string(buf[..j]);
        // common mishearings
        s = s.Replace("wreck", "req");
        s = s.Replace("recs", "reqs");
        s = s.Replace("rec", "req");
        // collapse whitespace
        return string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsListIntent(string[] tokens)
    {
        if (tokens.Length == 0) return false;
        var verbs = new HashSet<string>(["list", "show", "display"]);
        var nouns = new HashSet<string>(["req", "reqs", "requirement", "requirements"]);
        // find a verb followed by a noun anywhere
        for (int i = 0; i < tokens.Length; i++)
        {
            if (!verbs.Contains(tokens[i])) continue;
            for (int k = i + 0; k < tokens.Length; k++)
            {
                if (nouns.Contains(tokens[k])) return true;
            }
        }
        return false;
    }

    private static bool IsReadIntent(string[] tokens)
    {
        if (tokens.Length == 0) return false;
        // explicit read command
        if (tokens[0] == "read") return true;
        // or begins with noun + number words/digits ? treat as read
        if (tokens[0] is "req" or "requirement" or "requirements")
        {
            // if any following token is digit or number word, it's a read intent
            for (int i = 1; i < tokens.Length; i++)
            {
                if (HasDigit(tokens[i]) || NumberWordToDigit(tokens[i]) is not null)
                    return true;
            }
        }
        return false;
    }

    private static bool HasDigit(string s)
    {
        foreach (var c in s) if (char.IsDigit(c)) return true; return false;
    }

    private static string? ExtractRequirementId(string[] tokens)
    {
        // collect up to3 digits from subsequent tokens (digits or words)
        var digits = new List<char>(3);
        // skip leading verb if present
        int i = 0;
        if (i < tokens.Length && tokens[i] == "read") i++;
        if (i < tokens.Length && (tokens[i] == "req" || tokens[i] == "requirement" || tokens[i] == "requirements")) i++;
        for (; i < tokens.Length && digits.Count < 3; i++)
        {
            var t = tokens[i];
            foreach (var c in t)
            {
                if (char.IsDigit(c)) digits.Add(c);
            }
            if (digits.Count < 3)
            {
                var d = NumberWordToDigit(t);
                if (d is not null) digits.Add(d.Value);
            }
        }
        if (digits.Count == 0) return null;
        while (digits.Count < 3) digits.Insert(0, '0');
        if (digits.Count > 3) digits.RemoveRange(0, digits.Count - 3);
        return new string(digits.ToArray());
    }

    private static char? NumberWordToDigit(string t) => t switch
    {
        "zero" or "oh" or "o" => '0',
        "one" or "won" => '1',
        "two" or "to" or "too" => '2',
        "three" or "tree" => '3',
        "four" or "for" => '4',
        "five" => '5',
        "six" => '6',
        "seven" => '7',
        "eight" or "ate" => '8',
        "nine" => '9',
        _ => null
    };

    private static string NormalizeReqFilename(string raw)
    {
        var up = raw.ToUpperInvariant().Replace(".MD", ".md", StringComparison.Ordinal);
        // ensure REQ-XXX.md with3 digits
        var digits = new string(up.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return "REQ-000.md";
        if (digits.Length > 3) digits = digits[^3..];
        var id = digits.PadLeft(3, '0');
        return $"REQ-{id}.md";
    }
}
