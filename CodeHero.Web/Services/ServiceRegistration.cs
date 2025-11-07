namespace CodeHero.Web.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddAgentServices(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddSingleton<IMcpClient, McpClient>();
        // Register the LLM-based orchestrator and then the helper-routing wrapper
        services.AddSingleton<LlmOrchestratorAgentService>();
        services.AddSingleton<HelperRoutingAgentService>();
        // Expose the routing wrapper as the IAgentService implementation
        services.AddSingleton<IAgentService>(sp => sp.GetRequiredService<HelperRoutingAgentService>());
        return services;
    }
}