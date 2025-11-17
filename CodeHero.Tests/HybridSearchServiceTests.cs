using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeHero.Tests
{
    [TestClass]
    public class HybridSearchServiceTests
    {
        [TestMethod]
        public async Task Search_ReturnsHits_WhenEmbeddingProvided()
        {
            // Arrange
            var embedder = new DummyEmbedder(new float[] { 0.1f, 0.2f, 0.3f });
            var hits = new System.Collections.Generic.List<CodeHero.ApiService.Contracts.SearchHit>
            {
                new CodeHero.ApiService.Contracts.SearchHit("text-one","path/one.cs", 0.9),
                new CodeHero.ApiService.Contracts.SearchHit("text-two","path/two.cs", 0.8)
            };

            System.Func<CodeHero.ApiService.Contracts.SearchRequest, System.Threading.CancellationToken, System.Threading.Tasks.Task<CodeHero.ApiService.Contracts.SearchResponse>> fakeSearch = (req, ct) =>
                System.Threading.Tasks.Task.FromResult(new CodeHero.ApiService.Contracts.SearchResponse(hits));

            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<CodeHero.ApiService.Services.Rag.HybridSearchService>.Instance;
            var svc = new CodeHero.ApiService.Services.Rag.HybridSearchService(fakeSearch, embedder, logger);

            var reqObj = new CodeHero.ApiService.Contracts.SearchRequest("how do I do X?", TopK: 2);

            // Act
            var resp = await svc.SearchAsync(reqObj);

            // Assert
            Assert.IsNotNull(resp);
            Assert.AreEqual(2, resp.Results.Count);
            Assert.AreEqual("text-one", resp.Results[0].Content);
            Assert.AreEqual("path/one.cs", resp.Results[0].Source);
            Assert.AreEqual(0.9, resp.Results[0].Score);
        }

        [TestMethod]
        public async Task Search_ReturnsEmpty_WhenEmbeddingMissing()
        {
            // Arrange: embedder returns null to simulate missing embedding/config
            var embedder = new DummyEmbedder(null);
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<CodeHero.ApiService.Services.Rag.HybridSearchService>.Instance;
            var svc = new CodeHero.ApiService.Services.Rag.HybridSearchService((r, ct) => System.Threading.Tasks.Task.FromResult(new CodeHero.ApiService.Contracts.SearchResponse(new System.Collections.Generic.List<CodeHero.ApiService.Contracts.SearchHit>())), embedder, logger);
            var reqObj = new CodeHero.ApiService.Contracts.SearchRequest("no embed", TopK: 3);

            // Act
            var resp = await svc.SearchAsync(reqObj);

            // Assert
            Assert.IsNotNull(resp);
            Assert.AreEqual(0, resp.Results.Count);
        }

        private sealed class DummyEmbedder : CodeHero.ApiService.Services.Rag.IEmbeddingProvider
        {
            private readonly float[]? _vec;
            public DummyEmbedder(float[]? vec) => _vec = vec;
            public System.Threading.Tasks.Task<float[]?> GetEmbeddingAsync(string input, System.Threading.CancellationToken ct = default)
                => System.Threading.Tasks.Task.FromResult<float[]?>(_vec);
        }
    }
}
