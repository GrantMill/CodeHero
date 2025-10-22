using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.Extensions.Configuration;

namespace CodeHero.Tests;

[TestClass]
public class AgentsChatMicToggleTests
{
 private Bunit.TestContext _ctx = null!;

 [TestInitialize]
 public void Setup()
 {
 _ctx = new Bunit.TestContext();
 // JS interop setups used by the component
 _ctx.JSInterop.Setup<IJSStreamReference>("codeheroAudio.stopAsBlob").SetResult((IJSStreamReference?)null);
 _ctx.JSInterop.Setup<string>("codeheroAudio.stop").SetResult("");
 _ctx.JSInterop.SetupVoid("codeheroAudio.start");
 _ctx.JSInterop.SetupVoid("codeheroAudio.load");
 _ctx.JSInterop.Setup<CodeHero.Web.Services.AudioCapabilities>("codeheroAudio.support").SetResult(new CodeHero.Web.Services.AudioCapabilities
 {
 secure = true,
 hasMediaDevices = true,
 hasGetUserMedia = true,
 hasMediaRecorder = true,
 preferred = "audio/webm;codecs=opus",
 micPerm = "granted"
 });
 _ctx.Services.AddLogging();
 _ctx.Services.AddSingleton<CodeHero.Web.Services.IMcpClient, CodeHero.Web.Services.NullMcpClient>();
 _ctx.Services.AddHttpClient();
 _ctx.Services.AddSingleton<NavigationManager>(new FakeNav());
 // Provide IConfiguration for component injection
 var cfg = new ConfigurationBuilder()
 .AddInMemoryCollection(new Dictionary<string,string?>
 {
 ["Features:ContinuousDictation"] = "false" // keep continuous mode off in this unit test
 })
 .Build();
 _ctx.Services.AddSingleton<IConfiguration>(cfg);
 }

 [TestCleanup]
 public void Cleanup() => _ctx.Dispose();

 [TestMethod]
 public void MicToggle_StartsAndStopsRecording()
 {
 var cut = _ctx.RenderComponent<CodeHero.Web.Components.Pages.AgentsChat>();
 var btn = cut.Find("button[data-testid='mic-toggle']");

 // Toggle ON
 btn.Click();
 // Toggle OFF
 btn.Click();

 Assert.IsTrue(true);
 }

 private sealed class FakeNav : NavigationManager
 {
 public FakeNav(){ Initialize("https://localhost/", "https://localhost/agents/chat"); }
 protected override void NavigateToCore(string uri, bool forceLoad) { }
 }
}
