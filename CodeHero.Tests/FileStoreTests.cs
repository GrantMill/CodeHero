using CodeHero.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace CodeHero.Tests;

[TestClass]
public class FileStoreTests
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

    private static FileStore CreateStore(string root)
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["ContentRoots:Requirements"] = Path.Combine(root, "req"),
            ["ContentRoots:Architecture"] = Path.Combine(root, "arch"),
            ["ContentRoots:Artifacts"] = Path.Combine(root, "art"),
            ["ContentRoots:HumanPlan"] = Path.Combine(root, "HUMAN.md"),
            ["ContentRoots:AgentPlan"] = Path.Combine(root, "AGENT.md"),
        };
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(inMemory!).Build();
        var env = new TestWebEnv { ContentRootPath = root, ContentRootFileProvider = new PhysicalFileProvider(root) };
        return new FileStore(cfg, env);
    }

    [TestMethod]
    public void List_Filters_By_Extension()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            var reqDir = Path.Combine(temp.FullName, "req");
            Directory.CreateDirectory(reqDir);
            File.WriteAllText(Path.Combine(reqDir, "a.md"), "x");
            File.WriteAllText(Path.Combine(reqDir, "b.txt"), "x");

            var store = CreateStore(temp.FullName);
            var files = store.List(StoreRoot.Requirements, ".md").ToArray();

            CollectionAssert.AreEquivalent(new[] { "a.md" }, files);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Guard_Blocks_Path_Traversal()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(temp.FullName, "req"));
            var store = CreateStore(temp.FullName);
            // Attempt to escape directory
            store.WriteText(StoreRoot.Requirements, "..\\evil.md", "bad", ".md");
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }
}
