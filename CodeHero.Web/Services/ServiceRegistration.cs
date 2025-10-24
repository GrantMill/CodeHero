namespace CodeHero.Web.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddAgentServices(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddSingleton<IMcpClient, McpClient>();
        // Swap orchestrators here; default to LLM scaffold
        services.AddSingleton<IAgentService, LlmOrchestratorAgentService>();
        return services;
    }
}