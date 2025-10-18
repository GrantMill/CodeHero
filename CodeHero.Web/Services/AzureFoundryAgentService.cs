using Microsoft.Extensions.Configuration;

namespace CodeHero.Web.Services;

public sealed class AzureFoundryAgentService : IAgentService
{
    private readonly string _endpoint;
    private readonly string _key;

    public AzureFoundryAgentService(IConfiguration config)
    {
        _endpoint = config["AzureAI:Foundry:Endpoint"] ?? string.Empty;
        _key = config["AzureAI:Foundry:Key"] ?? string.Empty;
    }

    public Task<string> ChatAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_endpoint) || string.IsNullOrWhiteSpace(_key))
        {
            // Not configured; act as a no-op but indicate it's not wired.
            return Task.FromResult("Azure Foundry Agent not configured.");
        }

        // TODO: Implement call to Azure AI Foundry once model/route is defined.
        return Task.FromResult("[foundry/stub] " + input);
    }
}
