namespace CodeHero.Web.Services;

public interface IAgentService
{
    Task<string> ChatAsync(string input, CancellationToken ct = default);
}
