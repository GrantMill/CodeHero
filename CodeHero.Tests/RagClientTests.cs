using System;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Components;

namespace CodeHero.Tests
{
    [TestClass]
    public class RagClientTests
    {
        [TestMethod]
        public void Constructor_Reads_RagEnabled_False()
        {
            var inMemory = new System.Collections.Generic.Dictionary<string, string?>
            {
                ["Rag:Enabled"] = "false"
            };
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

            // Create a dummy HttpClient and NavigationManager replacement
            var http = new System.Net.Http.HttpClient { BaseAddress = new Uri("https://localhost/") };
            var nav = new TestNav();

            var client = new CodeHero.Web.Services.RagClient(http, nav, cfg);

            // Use reflection to access private field _ragEnabled
            var fi = typeof(CodeHero.Web.Services.RagClient).GetField("_ragEnabled", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(fi, "_ragEnabled field not found via reflection");
            var val = (bool)fi!.GetValue(client)!;
            Assert.IsFalse(val, "RagClient should read Rag:Enabled=false from configuration and set _ragEnabled to false");
        }

        private sealed class TestNav : NavigationManager
        {
            public TestNav()
            {
                Initialize("https://localhost/","https://localhost/");
            }

            protected override void NavigateToCore(string uri, bool forceLoad)
            {
                // no-op
            }
        }
    }
}
