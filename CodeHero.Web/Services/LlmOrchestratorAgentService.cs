using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CodeHero.Web.Services;

public sealed class LlmOrchestratorAgentService : IAgentService
{
    private readonly IMcpClient _mcp;
    private readonly ILogger<LlmOrchestratorAgentService> _log;
    private readonly IHttpClientFactory _http;
    private readonly string _endpoint; // base or full models route
    private readonly string _key;
    private readonly string _deployment; // for deployments route
    private readonly string _model; // for models route
    private readonly string _apiVersion; // used for deployments route
    private readonly bool _useModelsRoute;

    public LlmOrchestratorAgentService(IMcpClient mcp, IConfiguration cfg, ILogger<LlmOrchestratorAgentService> log, IHttpClientFactory http)
    {
        _mcp = mcp; _log = log; _http = http;
        var ep = (cfg["AzureAI:Foundry:Endpoint"] ?? string.Empty).TrimEnd('/');
        _endpoint = ep;
        _key = cfg["AzureAI:Foundry:Key"] ?? string.Empty;
        _deployment = cfg["AzureAI:Foundry:ChatDeployment"] ?? cfg["AzureAI:Foundry:Deployment"] ?? string.Empty;
        _model = cfg["AzureAI:Foundry:Model"] ?? string.Empty;
        _apiVersion = cfg["AzureAI:Foundry:ApiVersion"] ?? "2024-08-01-preview";
        _useModelsRoute = ep.Contains("/models", StringComparison.OrdinalIgnoreCase);

        // Sensible default model if models route is used without explicit model
        if (_useModelsRoute && string.IsNullOrWhiteSpace(_model))
        {
            _model = "gpt-4o-mini";
        }
        // Sensible default deployment if deployments route is used without explicit deployment
        if (!_useModelsRoute && string.IsNullOrWhiteSpace(_deployment))
        {
            _deployment = "gpt-4o-mini";
        }
    }

    public async Task<string> ChatAsync(string text, CancellationToken ct = default)
    {
        using var activity = AgentTelemetry.Activity.StartActivity("orchestrator.chat");
        try
        {
            var plan = await ComposePlanAsync(text, ct);
            if (plan.Steps.Count ==0)
                return await AnswerDirectAsync(text, ct);

            _log.LogInformation("Agent plan steps: {Steps}", string.Join(", ", plan.Steps.Select(s => s.Tool)));

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

    private async Task<Plan> ComposePlanAsync(string input, CancellationToken ct)
    {
        // Short-circuit: create intent should be deterministic
        var createPlan = TryHeuristicCreate(input);
        if (createPlan is not null)
            return createPlan;

        // Prefer LLM when configured; otherwise heuristic fallback
        if (IsFoundryConfigured)
        {
            try
            {
                using var act = AgentTelemetry.Activity.StartActivity("plan.llm");
                var sys = GetSystemPrompt();
                var json = await ChatJsonAsync(sys, input, ct);
                var plan = JsonSerializer.Deserialize<Plan>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Plan();
                // basic validation: tool allowlist
                plan.Steps = plan.Steps.Where(s => s is not null && IsAllowedTool(s.Tool)).ToList();
                return plan;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "LLM planning failed, falling back to heuristic");
            }
        }
        return ComposeHeuristicPlan(input);
    }

    private bool IsFoundryConfigured
        => !string.IsNullOrWhiteSpace(_key) &&
           (_useModelsRoute
                ? !string.IsNullOrWhiteSpace(_endpoint) && !string.IsNullOrWhiteSpace(_model)
                : !string.IsNullOrWhiteSpace(_endpoint) && !string.IsNullOrWhiteSpace(_deployment));

    private static bool IsAllowedTool(string? name)
        => name is "fs/list" or "fs/readText" or "fs/readLast" or "fs/count" or "scribe/createRequirement";

    private static Plan? TryHeuristicCreate(string input)
    {
        string Normalize(string s) => new string(s.ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) || ch == '-' || ch == '.').ToArray()).Trim();
        var text = Normalize(input);
        if (text.StartsWith("create") || text.StartsWith("add") || text.Contains("new requirement") || (text.Contains("requirement") && text.Contains("new")))
        {
            var title = BuildTitle(input, "REQ-000");
            return new Plan { Steps = { new PlanStep { Tool = "scribe/createRequirement", Parameters = JsonSerializer.SerializeToElement(new { id = "REQ-000", title }) } } };
        }
        return null;
    }

    private static int? ParseLimit(string text)
    {
        text = " " + text + " ";
        // numeric digits anywhere
        var digits = new string(text.Where(char.IsDigit).ToArray());
        if (!string.IsNullOrEmpty(digits) && int.TryParse(digits, out var n) && n >0)
            return n;
        // word numbers
        var map = WordNumbers;
        foreach (var kv in map)
        {
            if (text.Contains(" " + kv.Key + " ", StringComparison.OrdinalIgnoreCase)) return kv.Value;
        }
        return null;
    }

    private static readonly Dictionary<string, int> WordNumbers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"] =0, ["one"] =1, ["two"] =2, ["three"] =3, ["four"] =4, ["five"] =5,
        ["six"] =6, ["seven"] =7, ["eight"] =8, ["nine"] =9, ["ten"] =10
    };

    private static int? ExtractSingleRequirementNumber(string input)
    {
        // word number
        foreach (var kv in WordNumbers)
        {
            if (input.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >=0)
                return kv.Value;
        }
        // digits
        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (!string.IsNullOrEmpty(digits) && int.TryParse(digits, out var n))
            return n;
        return null;
    }

    private static bool ContainsPluralRequirements(string text)
        => text.Contains("requirements", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("all requirement", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("all requirements", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsSingularRequirementOnly(string text)
    {
        var lower = " " + text.ToLowerInvariant() + " ";
        // require word-boundary-ish match to avoid matching 'requirements'
        return lower.Contains(" requirement ") || lower.Contains(" requirement.") || lower.Contains(" requirement,") || lower.Contains(" requirement?");
    }

    private static Plan ComposeHeuristicPlan(string input)
    {
        string Normalize(string s) => new string(s.ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) || ch == '-' || ch == '.').ToArray()).Trim();
        var text = Normalize(input);
        if (string.IsNullOrWhiteSpace(text)) return new Plan();

        // read last requirement
        if (text.Contains("last", StringComparison.OrdinalIgnoreCase) && text.Contains("requirement", StringComparison.OrdinalIgnoreCase))
        {
            return new Plan { Steps = { new PlanStep { Tool = "fs/readLast", Parameters = JsonSerializer.SerializeToElement(new { root = "requirements" }) } } };
        }

        // count requirements
        if (text.Contains("how many", StringComparison.OrdinalIgnoreCase) && ContainsPluralRequirements(input))
        {
            return new Plan { Steps = { new PlanStep { Tool = "fs/count", Parameters = JsonSerializer.SerializeToElement(new { root = "requirements", exts = new[] { ".md" } }) } } };
        }

        // list top/first N requirements even without explicit 'list/show'
        if (ContainsPluralRequirements(input) && (text.Contains("top") || text.Contains("first") || text.Contains("limit")))
        {
            var limit = ParseLimit(input) ??2;
            var param = JsonSerializer.SerializeToElement(new { root = "requirements", exts = new[] { ".md" }, limit });
            return new Plan { Steps = { new PlanStep { Tool = "fs/list", Parameters = param } } };
        }

        // read specific requirement (even with 'show') – only when singular
        if ((text.Contains("read") || text.Contains("show")) && ContainsSingularRequirementOnly(input))
        {
            var num = ExtractSingleRequirementNumber(input);
            if (num.HasValue)
            {
                var name = $"REQ-{num.Value:D3}.md";
                return new Plan { Steps = { new PlanStep { Tool = "fs/readText", Parameters = JsonSerializer.SerializeToElement(new { root = "requirements", name }) } } };
            }
        }

        // list/show requirements, optionally with a limit like 'top two' or 'first3'
        if ((text.StartsWith("list") || text.StartsWith("show")) && ContainsPluralRequirements(input))
        {
            int? limit = null;
            if (text.Contains("top") || text.Contains("first") || text.Contains("limit"))
            {
                limit = ParseLimit(input);
            }
            var param = limit.HasValue
                ? JsonSerializer.SerializeToElement(new { root = "requirements", exts = new[] { ".md" }, limit = limit.Value })
                : JsonSerializer.SerializeToElement(new { root = "requirements", exts = new[] { ".md" } });
            return new Plan { Steps = { new PlanStep { Tool = "fs/list", Parameters = param } } };
        }

        // default read shortcut if phrase looks like singular requirement with a number
        if (ContainsSingularRequirementOnly(input))
        {
            var num = ExtractSingleRequirementNumber(input);
            if (num.HasValue)
            {
                var name = $"REQ-{num.Value:D3}.md";
                return new Plan { Steps = { new PlanStep { Tool = "fs/readText", Parameters = JsonSerializer.SerializeToElement(new { root = "requirements", name }) } } };
            }
        }

        // create/add new requirement (auto-increment handled by server)
        if (text.StartsWith("create") || text.StartsWith("add") || text.Contains("new requirement") || (text.Contains("requirement") && text.Contains("new")))
        {
            var title = BuildTitle(input, "REQ-000");
            return new Plan { Steps = { new PlanStep { Tool = "scribe/createRequirement", Parameters = JsonSerializer.SerializeToElement(new { id = "REQ-000", title }) } } };
        }

        return new Plan();
    }

    private static string BuildTitle(string input, string id)
    {
        // Remove common tokens and the id, keep the rest as title
        var title = input;
        foreach (var token in new[] { "create", "add", "new", "requirement", "req", "r-e-q", id, id.Replace("-", "") })
        {
            title = title.Replace(token, string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        // Remove used digits of id
        var digits = new string(id.Where(char.IsDigit).ToArray());
        if (!string.IsNullOrEmpty(digits)) title = title.Replace(digits, string.Empty);
        // Collapse spaces
        var parts = title.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cleaned = string.Join(' ', parts);
        return string.IsNullOrWhiteSpace(cleaned) ? "New Requirement" : cleaned;
    }

    private string GetSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are the Orchestrator. Plan a minimal sequence of tool calls to satisfy the user request.");
        sb.AppendLine("Rules:");
        sb.AppendLine("- Only use the allowed tools listed below.");
        sb.AppendLine("- Prefer the fewest steps; avoid redundant reads.");
        sb.AppendLine("- For list/show requirements: call fs/list with root=\"requirements\" and exts=[\".md\"].");
        sb.AppendLine("- For 'read requirement N' or 'read REQ-###': call fs/readText with name like 'REQ-###.md' and root=\"requirements\".");
        sb.AppendLine("- For 'read the last requirement': call fs/readLast.");
        sb.AppendLine("- To count requirements: call fs/count.");
        sb.AppendLine("- To create a requirement, use scribe/createRequirement with id and title.");
        sb.AppendLine("- Never write files unless explicitly asked.");
        sb.AppendLine("- Output strictly JSON matching this schema: { \"steps\": [ { \"tool\": string, \"parameters\": object } ] } with no extra fields or prose.");
        sb.AppendLine();
        sb.AppendLine("Allowed tools (allowlist):");
        sb.AppendLine("- fs/list: { root: 'requirements', exts?: string[] } -> returns { files: string[] }");
        sb.AppendLine("- fs/readText: { root: 'requirements', name: string } -> returns { text: string }");
        sb.AppendLine("- fs/readLast: {} -> returns { text: string }");
        sb.AppendLine("- fs/count: {} -> returns { count: number }");
        sb.AppendLine("- scribe/createRequirement: { id: 'REQ-###', title: string } -> returns { created: string }");
        return sb.ToString();
    }

    private async Task<string> ChatJsonAsync(string systemPrompt, string userText, CancellationToken ct)
    {
        if (!IsFoundryConfigured)
            throw new InvalidOperationException("Azure Foundry not configured.");

        using var client = _http.CreateClient();
        client.DefaultRequestHeaders.Add("api-key", _key);

        object payload = new
        {
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userText }
            },
            temperature =0,
            response_format = new { type = "json_object" },
            model = _useModelsRoute ? _model : null
        };

        string url = _useModelsRoute
            ? _endpoint // full models route provided by configuration (may include api-version)
            : $"{_endpoint}/openai/deployments/{_deployment}/chat/completions?api-version={_apiVersion}";

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await client.PostAsync(url, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Foundry chat failed: {(int)resp.StatusCode} {resp.ReasonPhrase} - {body}");
        using var doc = JsonDocument.Parse(body);
        var contentEl = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content");
        return contentEl.GetString() ?? "{}";
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
                    int limit = step.Parameters.TryGetProperty("limit", out var l) && l.TryGetInt32(out var li) && li >0 ? li : int.MaxValue;
                    var sliced = files.Take(limit).ToArray();
                    return sliced.Length ==0 ? "(none)" : string.Join("\n", sliced);
                }
            case "fs/count":
                {
                    var files = await _mcp.ListAsync(StoreRoot.Requirements, new[] { ".md" }, ct);
                    return files.Count.ToString();
                }
            case "fs/readText":
                {
                    var root = StoreRoot.Requirements;
                    var name = step.Parameters.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                    var body = await _mcp.ReadTextAsync(root, name, new[] { ".md" }, ct);
                    return $"--- {name} ---\n{body}";
                }
            case "fs/readLast":
                {
                    var files = await _mcp.ListAsync(StoreRoot.Requirements, new[] { ".md" }, ct);
                    // choose highest REQ-### numerically
                    string? last = files
                        .Select(f => new { File = f, Num = ExtractReqNumber(f) })
                        .OrderBy(x => x.Num)
                        .LastOrDefault()?.File;
                    if (string.IsNullOrEmpty(last)) return "(none)";
                    var body = await _mcp.ReadTextAsync(StoreRoot.Requirements, last, new[] { ".md" }, ct);
                    return $"--- {last} ---\n{body}";
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

    private static int ExtractReqNumber(string file)
    {
        var digits = new string(file.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : -1;
    }

    private Task<string> AnswerDirectAsync(string text, CancellationToken ct)
        => Task.FromResult($"[answer] {text}");

    private sealed class Plan
    {
        public List<PlanStep> Steps { get; set; } = new();
    }
    private sealed class PlanStep
    {
        public string Tool { get; set; } = string.Empty;
        public JsonElement Parameters { get; set; }
    }
}
