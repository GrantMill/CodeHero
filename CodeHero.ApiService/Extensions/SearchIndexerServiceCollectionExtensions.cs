using CodeHero.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeHero.Extensions;

public static class SearchIndexerServiceCollectionExtensions
{
    public static IServiceCollection AddOptionalAzureSearchIndexer(this IServiceCollection services, IConfiguration configuration)
    {
        // Register the real Azure implementation only if settings exist
        var endpoint = configuration["Search:Endpoint"];
        var apiKey = configuration["Search:ApiKey"];

        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
        {
            services.AddSingleton<ISearchIndexerService, AzureSearchIndexerService>();
        }
        else
        {
            // fallback no-op so we do not break other functionality or tests
            services.AddSingleton<ISearchIndexerService, NoopSearchIndexerService>();
        }

        return services;
    }
}