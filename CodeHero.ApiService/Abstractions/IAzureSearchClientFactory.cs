using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;

public interface IAzureSearchClientFactory
{
    SearchIndexClient CreateIndexClient(Uri endpoint, AzureKeyCredential credential);

    SearchClient CreateSearchClient(Uri endpoint, string indexName, AzureKeyCredential credential);
}

public class DefaultAzureSearchClientFactory : IAzureSearchClientFactory
{
    public SearchIndexClient CreateIndexClient(Uri endpoint, AzureKeyCredential credential) =>
        new SearchIndexClient(endpoint, credential);

    public SearchClient CreateSearchClient(Uri endpoint, string indexName, AzureKeyCredential credential) =>
        new SearchClient(endpoint, indexName, credential);
}