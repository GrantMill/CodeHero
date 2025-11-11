using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using CodeHero.Indexer.Interfaces;
using CodeHero.Indexer.Models;
using Microsoft.Extensions.Configuration;

namespace CodeHero.Indexer.Clients;

public class CognitiveSearchClient : ISearchClient
{
    private readonly IConfiguration _config;
    private readonly SearchClient _client;

    public CognitiveSearchClient(IConfiguration config)
    {
        _config = config;
        var endpoint = config["Search:Endpoint"];
        var indexName = config["Search:IndexName"];
        var apiKey = config["Search:ApiKey"];
        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(indexName))
            throw new InvalidOperationException("Search endpoint/index/key not configured");

        _client = new SearchClient(new Uri(endpoint), indexName, new AzureKeyCredential(apiKey));
    }

    public async System.Threading.Tasks.Task UpsertAsync(IEnumerable<Passage> docs)
    {
        var docsArray = docs.Select(d =>
            new Dictionary<string, object>
            {
                ["id"] = d.Id,
                ["text"] = d.Text,
                ["source"] = d.Source,
                ["offset"] = d.Offset,
                ["hash"] = d.Hash,
                ["vector"] = d.Vector
            }
        ).ToArray();

        var actions = docsArray.Select(o => IndexDocumentsAction.Upload(o)).ToArray();
        var batch = IndexDocumentsBatch.Create(actions);
        var res = await _client.IndexDocumentsAsync(batch);
        Console.WriteLine($"[CognitiveSearch] Upserted {docs.Count()} docs");
    }

    public async System.Threading.Tasks.Task DeleteAsync(IEnumerable<string> ids)
    {
        var objs = ids.Select(id => new Dictionary<string, object> { ["id"] = id }).ToArray();
        var actions = objs.Select(o => IndexDocumentsAction.Delete(o)).ToArray();
        var batch = IndexDocumentsBatch.Create(actions);
        var res = await _client.IndexDocumentsAsync(batch);
        Console.WriteLine($"[CognitiveSearch] Deleted {ids.Count()} docs");
    }
}