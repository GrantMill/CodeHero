using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CodeHero.Tests
{
    [TestClass]
    public class EmbeddingProviderTests
    {
        private sealed class TestHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _res;
            public TestHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> res) => _res = res;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_res(request, cancellationToken));
        }

        [TestMethod]
        public async Task NoopEmbeddingProvider_ReturnsNull()
        {
            var p = new CodeHero.ApiService.Services.Rag.NoopEmbeddingProvider();
            var emb = await p.GetEmbeddingAsync("hello");
            Assert.IsNull(emb);
        }

        [TestMethod]
        public async Task FoundryEmbeddingProvider_ReturnsEmbedding_WhenConfigured()
        {
            var responseJson = "{\"data\":[{\"embedding\":[0.1,0.2,0.3]}]}";
            var handler = new TestHandler((req, ct) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("https://foundry.example/") };

            var mockFactory = new Mock<System.Net.Http.IHttpClientFactory>();
            mockFactory.Setup(f => f.CreateClient("foundry")).Returns(client);

            var inMemory = new Dictionary<string, string?>
            {
                ["AzureAI:Foundry:Endpoint"] = "https://foundry.example/",
                ["AzureAI:Foundry:Key"] = "fake-key",
                ["AzureAI:Foundry:EmbeddingModel"] = "emb-1",
                ["AzureAI:Foundry:ApiVersion"] = "2024-08-01-preview"
            };

            var cfg = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
            var logger = new NullLogger<CodeHero.ApiService.Services.Rag.FoundryEmbeddingProvider>();

            var provider = new CodeHero.ApiService.Services.Rag.FoundryEmbeddingProvider(mockFactory.Object, cfg, logger);
            var emb = await provider.GetEmbeddingAsync("hello");

            Assert.IsNotNull(emb);
            Assert.AreEqual(3, emb!.Length);
            Assert.AreEqual(0.1f, emb[0]);
        }
    }
}
