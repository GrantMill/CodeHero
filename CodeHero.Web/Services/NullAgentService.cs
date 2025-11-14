namespace CodeHero.Web.Services;

public sealed class NullAgentService : IAgentService
{
    public Task<string> ChatAsync(string input, IReadOnlyList<ChatTurn>? chatHistory = null, CancellationToken ct = default)
    {
        // Echo service for dev/tests.
        return Task.FromResult($"[null-agent] {input}");
    }
}