using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Text;

namespace CodeHero.Tests;

[TestClass]
public class AgentsChatOnPhraseTests
{
    private Bunit.TestContext _ctx = null!;

    [TestInitialize]
    public void Setup()
    {
        _ctx = new Bunit.TestContext();
        // JS interop used by LoadAudioTag
        _ctx.JSInterop.SetupVoid("codeheroAudio.load");
        // Minimal DI
        _ctx.Services.AddLogging();
        _ctx.Services.AddSingleton<CodeHero.Web.Services.IMcpClient, CodeHero.Web.Services.NullMcpClient>();
        // Provide IHttpClientFactory backed by a test handler
        var handler = new StubHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        _ctx.Services.AddSingleton<IHttpClientFactory>(new Factory(client));
        // Navigation + config
        _ctx.Services.AddSingleton<NavigationManager>(new FakeNav());
        var cfg = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Features:ContinuousDictation"] = "true"
        })
        .Build();
        _ctx.Services.AddSingleton<IConfiguration>(cfg);
    }

    [TestCleanup]
    public void Cleanup() => _ctx.Dispose();

    [TestMethod, Timeout(60000)]
    public async Task OnPhrase_AddsYouAndAgentMessages()
    {
        var cut = _ctx.RenderComponent<CodeHero.Web.Components.Pages.AgentsChat>();
        var b64 = Convert.ToBase64String(Encoding.ASCII.GetBytes("fake-wav"));
        // Invoke OnPhrase via reflection (method is defined in Razor partial class)
        var mi = cut.Instance.GetType().GetMethod("OnPhrase", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(mi, "OnPhrase method not found on AgentsChat");
        var task = mi!.Invoke(cut.Instance, new object[] { b64 }) as Task;
        Assert.IsNotNull(task);
        // Await the task with an explicit timeout to avoid default test harness 10s timeout
        await task!.WaitAsync(TimeSpan.FromSeconds(30));
        // Assert chat contains both roles
        var html = cut.Markup;
        StringAssert.Contains(html, ">you:<");
        StringAssert.Contains(html, ">agent:<");
    }

    private sealed class Factory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public Factory(HttpClient client) => _client = client;

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class FakeNav : NavigationManager
    {
        public FakeNav() => Initialize("https://localhost/", "https://localhost/agents/chat");

        protected override void NavigateToCore(string uri, bool forceLoad)
        { }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/api/stt", StringComparison.OrdinalIgnoreCase))
            {
                // Return a phrase that triggers list intent
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("list requirements")
                });
            }
            if (path.EndsWith("/api/agent/chat", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("REQ-000.md\nREQ-001.md")
                });
            }
            if (path.StartsWith("/api/tts", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { 0, 1, 2 })
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}