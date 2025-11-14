namespace CodeHero.Web.Services;

public interface IAgentService
{
    Task<string> ChatAsync(string text, IReadOnlyList<ChatTurn>? chatHistory = null, CancellationToken ct = default);
}