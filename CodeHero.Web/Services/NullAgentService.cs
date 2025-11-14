namespace CodeHero.Web.Services;

/// <summary>
/// Development/test agent that echoes the input prefixed with a tag. Useful when no real agent is configured.
/// </summary>
public sealed class NullAgentService : IAgentService
{
    /// <inheritdoc />
    public Task<string> ChatAsync(string input, IReadOnlyList<ChatTurn>? chatHistory = null, CancellationToken ct = default)
    {
        // Echo service for dev/tests.
        return Task.FromResult($"[null-agent] {input}");
    }
}