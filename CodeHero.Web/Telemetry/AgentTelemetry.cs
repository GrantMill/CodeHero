using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CodeHero.Web;

public static class AgentTelemetry
{
    public static readonly ActivitySource Activity = new("CodeHero.Agent");
    private static readonly Meter Meter = new("CodeHero.Agent");

    public static readonly Histogram<double> StepDurationMs = Meter.CreateHistogram<double>("agent_step_duration_ms", unit: "ms", description: "Duration of each plan step");
    public static readonly Counter<long> Steps = Meter.CreateCounter<long>("agent_steps_total", description: "Number of executed plan steps");
    public static readonly Counter<long> Errors = Meter.CreateCounter<long>("agent_errors_total", description: "Agent orchestration errors");
    public static readonly Counter<long> WritesBlocked = Meter.CreateCounter<long>("agent_writes_blocked_total", description: "Write operations blocked by policy");
}
