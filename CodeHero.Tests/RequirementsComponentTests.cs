using Bunit;
using CodeHero.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace CodeHero.Tests;

[TestClass]
public class RequirementsComponentTests
{
    private sealed class TestWebEnv : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(Directory.GetCurrentDirectory());
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public string EnvironmentName { get; set; } = "Development";
        public IFileProvider WebRootFileProvider { get; set; } = new PhysicalFileProvider(Directory.GetCurrentDirectory());
        public string? WebRootPath { get; set; } = null;
    }

    private static FileStore CreateStore(string root, string fileName, string content)
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["ContentRoots:Requirements"] = Path.Combine(root, "req"),
            ["ContentRoots:Architecture"] = Path.Combine(root, "arch"),
            ["ContentRoots:Features"] = Path.Combine(root, "features"),
            ["ContentRoots:Artifacts"] = Path.Combine(root, "art"),
            ["ContentRoots:Backlog"] = Path.Combine(root, "BACKLOG.md"),
            ["ContentRoots:HumanPlan"] = Path.Combine(root, "HUMAN.md"),
            ["ContentRoots:AgentPlan"] = Path.Combine(root, "AGENT.md"),
        };
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(inMemory!).Build();
        var env = new TestWebEnv { ContentRootPath = root, ContentRootFileProvider = new PhysicalFileProvider(root) };

        // Seed requirement file
        var reqDir = Path.Combine(root, "req");
        Directory.CreateDirectory(reqDir);
        File.WriteAllText(Path.Combine(reqDir, fileName), content);

        return new FileStore(cfg, env);
    }

    [TestMethod]
    public void Clicking_File_Loads_Content()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            var store = CreateStore(temp.FullName, "REQ-000.md", "hello from test");

            using var ctx = new Bunit.TestContext();
            ctx.Services.AddSingleton(store);

            var cut = ctx.RenderComponent<CodeHero.Web.Components.Pages.Requirements>();

            // click first file button
            var button = cut.Find("button.btn-link");
            button.Click();

            // textarea should appear with file contents
            var textarea = cut.Find("textarea");
            StringAssert.Contains(textarea.GetAttribute("value"), "hello from test");
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }
}
