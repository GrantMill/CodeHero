using CodeHero.ApiService.Contracts;
using CodeHero.ApiService.Services.Rag;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeHero.Tests;

[TestClass]
public class AoaiQuestionRephraserTests
{
    [TestMethod]
    public async Task TrivialInputProducesDeterministicGrounded()
    {
        var factory = new TestHttpClientFactory();
        var cfg = new ConfigurationBuilder().Build();
        var rephraser = new AoaiQuestionRephraser(factory, cfg, NullLogger<AoaiQuestionRephraser>.Instance);

        var req = new ChatRequest("help", Array.Empty<ChatTurn>());
        var outp = await rephraser.RephraseAsync(req, CancellationToken.None);
        StringAssert.Contains(outp, "For this repository");
        StringAssert.Contains(outp, "how do I use the application");
    }

    [TestMethod]
    public async Task NonTrivialCallsFactoryClientIfProvided()
    {
        // create a factory whose client will return a JSON choice
        var factory = new TestHttpClientFactory("{\"choices\":[{\"message\":{\"content\":\"Rephrased question from model\"}}]}", HttpStatusCode.OK);
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string,string?> { ["AOAI:Endpoint"] = "https://example.local", ["AOAI:Key"] = "key", ["AOAI:ChatDeployment"] = "Phi-4" })
            .Build();
        var rephraser = new AoaiQuestionRephraser(factory, cfg, NullLogger<AoaiQuestionRephraser>.Instance);

        var req = new ChatRequest("what's next", new List<ChatTurn> { new ChatTurn("previous context","", DateTimeOffset.UtcNow) });
        var outp = await rephraser.RephraseAsync(req, CancellationToken.None);
        Assert.AreEqual("Rephrased question from model", outp);
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public TestHttpClientFactory(string responseBody = "", HttpStatusCode status = HttpStatusCode.OK)
        {
            var handler = new StubHandler(responseBody, status);
            _client = new HttpClient(handler) { BaseAddress = new Uri("https://example.local") };
        }

        public HttpClient CreateClient(string name) => _client;

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly string _body;
            private readonly HttpStatusCode _status;
            public StubHandler(string body, HttpStatusCode status) { _body = body; _status = status; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body) });
            }
        }
    }
}
