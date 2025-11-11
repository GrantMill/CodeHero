using CodeHero.ApiService.Contracts;

namespace CodeHero.ApiService.Services.Rag;

public interface IHybridSearchService
{
    Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken ct = default);
}