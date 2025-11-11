using CodeHero.ApiService.Contracts;

namespace CodeHero.ApiService.Services.Rag;

internal sealed class FakeHybridSearchService : IHybridSearchService
{
    public Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken ct = default)
        => Task.FromResult(new SearchResponse(Array.Empty<SearchHit>()));
}