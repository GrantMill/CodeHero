using CodeHero.ApiService.Contracts;

namespace CodeHero.ApiService.Services.Rag;

/// <summary>
/// Minimal in-memory implementation of <see cref="IHybridSearchService"/> that returns no results.
/// Useful for development scenarios when Azure Search is not configured.
/// </summary>
internal sealed class FakeHybridSearchService : IHybridSearchService
{
    /// <inheritdoc />
    public Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken ct = default)
        => Task.FromResult(new SearchResponse(Array.Empty<SearchHit>()));
}