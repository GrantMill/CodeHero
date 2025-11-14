namespace CodeHero.Web.Services;

/// <summary>
/// Defines an abstraction for agent chat services capable of generating responses based on user input
/// and optional chat history. Implementations may route to MCP tools, RAG pipelines, or LLM backends.
/// </summary>
public interface IAgentService
{
    /// <summary>
    /// Produces an agent reply to the provided <paramref name="text"/> considering optional <paramref name="chatHistory"/>.
    /// </summary>
    /// <param name="text">The latest user input to act on.</param>
    /// <param name="chatHistory">Optional previous chat turns to provide context to the agent.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task resolving to the agent reply as plain text.</returns>
    Task<string> ChatAsync(string text, IReadOnlyList<ChatTurn>? chatHistory = null, CancellationToken ct = default);
}