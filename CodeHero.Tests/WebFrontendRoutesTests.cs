using Microsoft.Extensions.Logging;

namespace CodeHero.Tests;

[TestClass]
public class WebFrontendRoutesTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private static async Task<HttpClient> StartAsync(CancellationToken ct)
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CodeHero_AppHost>(ct);
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Debug);
        });
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
            clientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        });

        var app = await appHost.BuildAsync(ct).WaitAsync(DefaultTimeout, ct);
        await app.StartAsync(ct).WaitAsync(DefaultTimeout, ct);

        var httpClient = app.CreateHttpClient("webfrontend");
        await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", ct).WaitAsync(DefaultTimeout, ct);
        return httpClient;
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RequirementsRoute_Renders()
    {
        using var cts = new CancellationTokenSource(DefaultTimeout);
        var client = await StartAsync(cts.Token);
        var resp = await client.GetAsync("/requirements", cts.Token);
        var html = await resp.Content.ReadAsStringAsync(cts.Token);

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        StringAssert.Contains(html, "Requirements");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ArchitectureRoute_Renders()
    {
        using var cts = new CancellationTokenSource(DefaultTimeout);
        var client = await StartAsync(cts.Token);
        var resp = await client.GetAsync("/architecture", cts.Token);
        var html = await resp.Content.ReadAsStringAsync(cts.Token);

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        StringAssert.Contains(html, "Architecture");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PlanRoute_Renders()
    {
        using var cts = new CancellationTokenSource(DefaultTimeout);
        var client = await StartAsync(cts.Token);
        var resp = await client.GetAsync("/plan", cts.Token);
        var html = await resp.Content.ReadAsStringAsync(cts.Token);

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        StringAssert.Contains(html, "Backlog");
    }
}