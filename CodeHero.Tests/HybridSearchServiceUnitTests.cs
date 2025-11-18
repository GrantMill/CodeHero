using CodeHero.ApiService.Services.Rag;
using Microsoft.Extensions.Logging.Abstractions;
using CodeHero.ApiService.Contracts;

namespace CodeHero.Tests;

[TestClass]
public class HybridSearchServiceUnitTests
{
    [TestMethod]
    public async Task Search_ReturnsEmpty_WhenEmbeddingMissing()
    {
        // Arrange: embedder returns null/empty
        var embedder = new TestEmbedder(returnEmpty: true);
        var log = NullLogger<HybridSearchService>.Instance;

        // Provide a search func that would throw if called (should not be called when embedding missing)
        Task<SearchResponse> SearchFunc(SearchRequest r, CancellationToken ct) => throw new InvalidOperationException("Search should not be invoked");

        var svc = new HybridSearchService(SearchFunc, embedder, log);

        // Act
        var res = await svc.SearchAsync(new SearchRequest("hello world", 5));

        // Assert
        Assert.IsNotNull(res);
        Assert.AreEqual(0, res.Results.Count);
    }

    [TestMethod]
    public async Task Search_UsesSearchFunc_WhenEmbeddingPresent()
    {
        // Arrange: embedder returns a deterministic vector
        var embedder = new TestEmbedder(returnEmpty: false);
        var log = NullLogger<HybridSearchService>.Instance;

        var called = false;
        Task<SearchResponse> SearchFunc(SearchRequest r, CancellationToken ct)
        {
            called = true;
            var hit = new SearchHit("content", "docs/README.md", 0.9);
            return Task.FromResult(new SearchResponse(new List<SearchHit> { hit }));
        }

        var svc = new HybridSearchService(SearchFunc, embedder, log);

        // Act
        var res = await svc.SearchAsync(new SearchRequest("query text", 3));

        // Assert
        Assert.IsTrue(called, "Expected search function to be called when embedding present");
        Assert.IsNotNull(res);
        Assert.AreEqual(1, res.Results.Count);
        Assert.AreEqual("docs/README.md", res.Results[0].Source);
    }

    private class TestEmbedder : IEmbeddingProvider
    {
        private readonly bool _empty;
        public TestEmbedder(bool returnEmpty) => _empty = returnEmpty;
        public Task<float[]?> GetEmbeddingAsync(string text, CancellationToken ct = default)
        {
            if (_empty) return Task.FromResult<float[]?>(Array.Empty<float>());
            return Task.FromResult<float[]?>(new float[] { 0.1f, 0.2f, 0.3f });
        }
    }
}
