using CodeHero.Indexer.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CodeHero.Indexer.Clients;

public class FoundryEmbeddingClient : IEmbeddingClient
{
 private readonly IConfiguration _config;
 private readonly IHttpClientFactory _http;
 public FoundryEmbeddingClient(IConfiguration config, IHttpClientFactory http)
 {
 _config = config;
 _http = http;
 }

 public float[] Embed(string text)
 {
 var endpoint = _config["Foundry:Endpoint"];
 var key = _config["Foundry:Key"];
 if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
 throw new InvalidOperationException("Foundry endpoint/key not configured");

 Console.WriteLine("[FoundryEmbeddingClient] placeholder - calling mock embedding for now");
 return new MockEmbeddingClient().Embed(text);
 }
}