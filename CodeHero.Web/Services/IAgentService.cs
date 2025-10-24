namespace CodeHero.Web.Services;

public interface IAgentService
{
    Task<string> ChatAsync(string text, CancellationToken ct = default);
}