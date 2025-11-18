using CodeHero.ApiService.Services.Rag;
using CodeHero.ApiService.Contracts;

namespace CodeHero.Tests;

[TestClass]
public class PromptContextBuilderTests
{
    [TestMethod]
    public void Build_IncludesSource_WhenProvided()
    {
        // Arrange
        var hits = new List<SearchHit>
        {
            new SearchHit("This is content.", "docs/requirements/req-001.md", 1.0f),
            new SearchHit("Other content.", "CodeHero.ApiService/Services/FileStore.cs:10-20", 0.8f)
        };

        // Act
        var ctx = PromptContextBuilder.Build(hits);

        // Assert
        Assert.IsTrue(ctx.Contains("Source: docs/requirements/req-001.md"));
        Assert.IsTrue(ctx.Contains("Source: CodeHero.ApiService/Services/FileStore.cs:10-20"));
    }
}
