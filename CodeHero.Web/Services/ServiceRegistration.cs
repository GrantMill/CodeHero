using CodeHero.Web.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddAgentServices(this IServiceCollection services, IConfiguration cfg)
    {
        // NOTE: The "foundry" HttpClient is configured centrally in Program.cs to ensure
        // consistent handler and timeout policy (HTTP/1.1, handler tuning). Do NOT re-register
        // the same named client here — registering it twice overrides the earlier configuration
        // and can re-enable HTTP/2 pings or shorter Polly timeouts that cause timeouts.

        // Keep local client registrations for other purposes if needed, but avoid duplicating
        // the 'foundry' named client registration.

        services.AddSingleton<IMcpClient, McpClient>();
        // Register the Foundry-backed agent (for direct chat) and the LLM-based orchestrator and helper
        services.AddSingleton<AzureFoundryAgentService>();
        services.AddSingleton<LlmOrchestratorAgentService>();
        services.AddSingleton<HelperRoutingAgentService>();
        // Expose the routing wrapper as the IAgentService implementation
        services.AddSingleton<IAgentService>(sp => sp.GetRequiredService<HelperRoutingAgentService>());
        return services;
    }
}