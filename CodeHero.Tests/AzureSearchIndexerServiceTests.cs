using Azure;
using Azure.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using CodeHero.Services;

namespace CodeHero.Tests;

[TestClass]
public class AzureSearchIndexerServiceTests
{
    [TestMethod]
    public async Task CreateIndexAndRunAsync_WhenIndexMissing_CreatesIndexAndUploads()
    {
        // Arrange: temp repo with one file
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "readme.md");
        await File.WriteAllTextAsync(file, "hello world");

        var inMemory = new Dictionary<string, string?>
        {
            ["Search:Endpoint"] = "https://fake",
            ["Search:ApiKey"] = "fake",
            ["Search:IndexName"] = "test-index",
            ["ContentRoot"] = root
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

        // Mock factory + clients
        var indexClientMock = new Mock<SearchIndexClient>(MockBehavior.Strict, new Uri("https://fake"), new AzureKeyCredential("fake"));
        var searchClientMock = new Mock<SearchClient>(MockBehavior.Strict, new Uri("https://fake"), "test-index", new AzureKeyCredential("fake"));

        // Simulate GetIndexAsync -> throw 404 so index is created
        indexClientMock
            .Setup(c => c.GetIndexAsync("test-index", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "NotFound"));

        // Expect CreateIndexAsync called once
        indexClientMock
            .Setup(c => c.CreateIndexAsync(It.IsAny<SearchIndex>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(new SearchIndex("test-index", new List<SearchField>()), Mock.Of<Response>()));

        // Expect IndexDocumentsAsync called at least once - return a Response<IndexDocumentsResult>
        var fakeIndexDocsResult = Mock.Of<IndexDocumentsResult>();
        var fakeIndexDocsResponse = Response.FromValue(fakeIndexDocsResult, Mock.Of<Response>());
        searchClientMock
            .Setup(c => c.IndexDocumentsAsync(It.IsAny<IndexDocumentsBatch<Dictionary<string, object>>>(), It.IsAny<IndexDocumentsOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeIndexDocsResponse);

        // Factory that returns mocks
        var factory = new Mock<IAzureSearchClientFactory>();
        factory.Setup(f => f.CreateIndexClient(It.IsAny<Uri>(), It.IsAny<AzureKeyCredential>())).Returns(indexClientMock.Object);
        factory.Setup(f => f.CreateSearchClient(It.IsAny<Uri>(), "test-index", It.IsAny<AzureKeyCredential>())).Returns(searchClientMock.Object);

        var svc = new AzureSearchIndexerService(config, NullLogger<AzureSearchIndexerService>.Instance, factory.Object);

        // Act
        var res = await svc.CreateIndexAndRunAsync(CancellationToken.None);

        // Assert
        Assert.IsTrue(res.Success);
        indexClientMock.Verify(c => c.CreateIndexAsync(It.IsAny<SearchIndex>(), It.IsAny<CancellationToken>()), Times.Once);
        searchClientMock.Verify(c => c.IndexDocumentsAsync(It.IsAny<IndexDocumentsBatch<Dictionary<string, object>>>(), It.IsAny<IndexDocumentsOptions>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        // cleanup
        Directory.Delete(root, true);
    }
}