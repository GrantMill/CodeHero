using System.Text.RegularExpressions;

namespace CodeHero.Web.Services;

/// <summary>
/// Simple rule-based orchestrator that maps user requests to allowed MCP tool calls via IMcpClient.
/// Keep the tool allowlist small and explicit.
/// </summary>
public sealed class McpOrchestratorAgentService : IAgentService
{
 private static readonly Regex ReadReq = new("^read\\s+(?<name>\\S+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
 private static readonly Regex CreateReq = new("^create\\s+(?<id>REQ-\\d{3,})\\s+(?<title>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

 private readonly IMcpClient _mcp;

 public McpOrchestratorAgentService(IMcpClient mcp) => _mcp = mcp;

 public async Task<string> ChatAsync(string text, CancellationToken ct = default)
 {
 var t = (text ?? string.Empty).Trim();
 if (string.IsNullOrEmpty(t)) return "(empty)";

 // Allowlist: fs/list, fs/readText, fs/writeText, scribe/createRequirement
 if (t.StartsWith("list req", StringComparison.OrdinalIgnoreCase))
 {
 var files = await _mcp.ListAsync(StoreRoot.Requirements, new[] { ".md" }, ct);
 return files.Count ==0 ? "No requirements found." : string.Join("\n", files);
 }

 var mRead = ReadReq.Match(t);
 if (mRead.Success)
 {
 var name = mRead.Groups["name"].Value;
 var body = await _mcp.ReadTextAsync(StoreRoot.Requirements, name, new[] { ".md" }, ct);
 return $"--- {name} ---\n{body}";
 }

 var mCreate = CreateReq.Match(t);
 if (mCreate.Success)
 {
 var id = mCreate.Groups["id"].Value;
 var title = mCreate.Groups["title"].Value.Trim();
 var created = await _mcp.ScribeCreateRequirementAsync(id, title, ct);
 return $"Created: {created}";
 }

 // Fallback: echo and guidance
 return "Sorry, I can: list req | read REQ-xxx.md | create REQ-xxx Title";
 }
}
