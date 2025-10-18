namespace CodeHero.Web.Services;

public sealed class NullAgentService : IAgentService
{
    public Task<string> ChatAsync(string input, CancellationToken ct = default)
    {
        // Echo service for dev/tests.
        return Task.FromResult($"[null-agent] {input}");
    }
}
