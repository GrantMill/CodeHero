using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CodeHero.Web.Services;

public sealed class LlmOrchestratorAgentService : IAgentService
{
    private readonly IMcpClient _mcp;
    private readonly ILogger<LlmOrchestratorAgentService> _log;
    private readonly string _provider;
    private readonly string _model;

    public LlmOrchestratorAgentService(IMcpClient mcp, IConfiguration cfg, ILogger<LlmOrchestratorAgentService> log)
    {
        _mcp = mcp; _log = log;
        _provider = cfg["AzureAI:Foundry:Endpoint"] ?? ""; // placeholder to signal LLM availability
        _model = cfg["AzureAI:Foundry:Model"] ?? "gpt-4o-mini";
    }

    public async Task<string> ChatAsync(string text, CancellationToken ct = default)
    {
        using var activity = AgentTelemetry.Activity.StartActivity("orchestrator.chat");
        try
        {
            var plan = await ComposePlanAsync(text, ct);
            if (plan.Steps.Count == 0)
                return await AnswerDirectAsync(text, ct);

            var results = new List<string>();
            foreach (var step in plan.Steps)
            {
                using var stepAct = AgentTelemetry.Activity.StartActivity(step.Tool);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var output = await ExecuteAsync(step, ct);
                    results.Add($"[{step.Tool}] {output}");
                    AgentTelemetry.Steps.Add(1);
                }
                catch (Exception ex)
                {
                    AgentTelemetry.Errors.Add(1);
                    results.Add($"[{step.Tool}] error: {ex.Message}");
                }
                finally
                {
                    sw.Stop(); AgentTelemetry.StepDurationMs.Record(sw.Elapsed.TotalMilliseconds);
                }
            }
            return string.Join("\n", results);
        }
        catch (Exception ex)
        {
            AgentTelemetry.Errors.Add(1);
            _log.LogError(ex, "LLM orchestrator failed");
            return $"Error: {ex.Message}";
        }
    }

    private static string Normalize(string s)
        => new string(s.ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) || ch == '-' || ch == '.').ToArray()).Trim();

    // Replace this with a real LLM call with tool schema and repo-grounding
    private Task<Plan> ComposePlanAsync(string input, CancellationToken ct)
    {
        var text = Normalize(input);
        if (string.IsNullOrWhiteSpace(text)) return Task.FromResult(new Plan());

        // list requirements variants
        if (text.StartsWith("list") || text.StartsWith("show"))
        {
            if (text.Contains("req") || text.Contains("requirement"))
            {
                return Task.FromResult(new Plan
                {
                    Steps = { new PlanStep { Tool = "fs/list", Parameters = JsonSerializer.SerializeToElement(new { root = "requirements", exts = new[] { ".md" } }) } }
                });
            }
        }

        // read requirement [n] or REQ-xxx.md
        if (text.StartsWith("read") || text.StartsWith("requirement"))
        {
            // extract REQ-xxx or number
            string name = "REQ-000.md";
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var joined = string.Join(' ', parts);
            var reqIdx = joined.IndexOf("req-");
            if (reqIdx >= 0)
            {
                // assume explicit filename or id
                var token = joined.Substring(reqIdx).Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                var digits = new string(token.Where(char.IsDigit).ToArray());
                if (digits.Length > 0)
                    name = $"REQ-{digits.PadLeft(3, '0')}.md";
            }
            else
            {
                // try to find a trailing number
                var digits = new string(joined.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
                if (!string.IsNullOrEmpty(digits)) name = $"REQ-{digits.PadLeft(3, '0')}.md";
            }
            return Task.FromResult(new Plan { Steps = { new PlanStep { Tool = "fs/readText", Parameters = JsonSerializer.SerializeToElement(new { root = "requirements", name }) } } });
        }

        // create requirement: create REQ-123 Title
        if (text.StartsWith("create"))
        {
            var rest = input.Substring(6).Trim();
            var space = rest.IndexOf(' ');
            if (space > 0)
            {
                var id = rest[..space];
                var title = rest[(space + 1)..];
                return Task.FromResult(new Plan { Steps = { new PlanStep { Tool = "scribe/createRequirement", Parameters = JsonSerializer.SerializeToElement(new { id, title }) } } });
            }
        }
        // default to direct answer (no tools)
        return Task.FromResult(new Plan());
    }

    private async Task<string> ExecuteAsync(PlanStep step, CancellationToken ct)
    {
        switch (step.Tool)
        {
            case "fs/list":
                {
                    var root = StoreRoot.Requirements;
                    var exts = new[] { ".md" };
                    var files = await _mcp.ListAsync(root, exts, ct);
                    return files.Count == 0 ? "(none)" : string.Join("\n", files);
                }
            case "fs/readText":
                {
                    var root = StoreRoot.Requirements;
                    var name = step.Parameters.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                    var body = await _mcp.ReadTextAsync(root, name, new[] { ".md" }, ct);
                    return $"--- {name} ---\n{body}";
                }
            case "scribe/createRequirement":
                {
                    var id = step.Parameters.TryGetProperty("id", out var i) ? i.GetString() ?? "REQ-001" : "REQ-001";
                    var title = step.Parameters.TryGetProperty("title", out var t) ? t.GetString() ?? "New Requirement" : "New Requirement";
                    var created = await _mcp.ScribeCreateRequirementAsync(id, title, ct);
                    return $"Created: {created}";
                }
            default:
                throw new NotSupportedException($"Unknown tool: {step.Tool}");
        }
    }

    private Task<string> AnswerDirectAsync(string text, CancellationToken ct)
        => Task.FromResult($"[answer] {text}");

    private sealed class Plan
    {
        public List<PlanStep> Steps { get; init; } = new();
    }
    private sealed class PlanStep
    {
        public string Tool { get; init; } = string.Empty;
        public JsonElement Parameters { get; init; }
    }
}
