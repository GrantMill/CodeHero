using CodeHero.Indexer.Clients;
using CodeHero.Indexer.Interfaces;
using CodeHero.Indexer.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var config = new ConfigurationBuilder()
 .AddEnvironmentVariables()
 .Build();

// Startup validation for real providers
var enableReal = config["USE_REAL_PROVIDERS"] == "1";
if (enableReal)
{
    var missing = new List<string>();
    if (string.IsNullOrWhiteSpace(config["Foundry:Endpoint"])) missing.Add("Foundry:Endpoint (env: Foundry__Endpoint / secret FOUNDRY_ENDPOINT)");
    if (string.IsNullOrWhiteSpace(config["Foundry:Key"])) missing.Add("Foundry:Key (env: Foundry__Key / secret FOUNDRY_KEY)");
    if (string.IsNullOrWhiteSpace(config["Foundry:Model"])) missing.Add("Foundry:Model (env: Foundry__Model / secret FOUNDRY_MODEL)");
    if (string.IsNullOrWhiteSpace(config["Search:Endpoint"])) missing.Add("Search:Endpoint (env: Search__Endpoint / secret SEARCH_ENDPOINT)");
    if (string.IsNullOrWhiteSpace(config["Search:IndexName"])) missing.Add("Search:IndexName (env: Search__IndexName / secret SEARCH_INDEX_NAME)");
    if (string.IsNullOrWhiteSpace(config["Search:ApiKey"])) missing.Add("Search:ApiKey (env: Search__ApiKey / secret SEARCH_API_KEY)");

    if (missing.Any())
    {
        Console.Error.WriteLine("ERROR: USE_REAL_PROVIDERS=1 but the following required settings are missing:");
        foreach (var m in missing) Console.Error.WriteLine(" - " + m);
        Console.Error.WriteLine("Set the corresponding GitHub Actions secrets or environment variables before enabling real providers.");
        // Exit with non-zero to fail CI fast
        Environment.Exit(2);
    }
}

var services = new ServiceCollection();
services.AddHttpClient();
services.AddSingleton<IConfiguration>(config);

// choose implementations based on env var
if (enableReal)
{
    // wire real providers
    services.AddSingleton<IEmbeddingClient, FoundryEmbeddingClient>();
    services.AddSingleton<ISearchClient, CognitiveSearchClient>();
}
else
{
    services.AddSingleton<IEmbeddingClient, MockEmbeddingClient>();
    services.AddSingleton<ISearchClient, MockSearchClient>();
}

var provider = services.BuildServiceProvider();
var embedding = provider.GetRequiredService<IEmbeddingClient>();
var search = provider.GetRequiredService<ISearchClient>();

Console.WriteLine("CodeHero Indexer - Phase2 (pluggable clients)");

// Use current directory as repo root when running published binary from repo root (CI)
var repoRoot = Directory.GetCurrentDirectory();
var docsDir = Path.Combine(repoRoot, "docs");
var outputDir = Path.Combine(repoRoot, "data");
Directory.CreateDirectory(outputDir);

var files = Directory.Exists(docsDir)
 ? Directory.GetFiles(docsDir, "*.*", SearchOption.AllDirectories)
 .Where(f => f.EndsWith(".md") || f.EndsWith(".mmd") || f.EndsWith(".txt") || f.EndsWith(".cs"))
 .ToArray()
 : Array.Empty<string>();

Console.WriteLine($"Found {files.Length} files under {docsDir}");

var passages = new List<Passage>();
var chunkTexts = new List<string>();

foreach (var file in files)
{
    var text = File.ReadAllText(file);
    var chunks = ChunkText(text, 1000);
    for (int i = 0; i < chunks.Count; i++)
    {
        var chunk = chunks[i];
        var id = GenerateId(file, i, chunk);
        var hash = ComputeHash(chunk);
        var p = new Passage
        {
            Id = id,
            Text = chunk,
            Source = Path.GetRelativePath(repoRoot, file).Replace("\\", "/"),
            Offset = i,
            Hash = hash,
        };
        passages.Add(p);
        chunkTexts.Add(chunk);
    }
}

// compute embeddings in batch
var allVectors = await embedding.EmbedBatchAsync(chunkTexts);
for (int i = 0; i < passages.Count && i < allVectors.Length; i++)
{
    passages[i].Vector = allVectors[i];
}

// incremental: compare with existing index file (if present)
var outPath = Path.Combine(outputDir, "indexed.json");
var existing = File.Exists(outPath) ? JsonSerializer.Deserialize<List<Passage>>(File.ReadAllText(outPath)) ?? new List<Passage>() : new List<Passage>();

var toUpsert = passages.Where(p => !existing.Any(e => e.Hash == p.Hash && e.Source == p.Source && e.Offset == p.Offset)).ToList();
var toDelete = existing.Where(e => !passages.Any(p => p.Hash == e.Hash && p.Source == e.Source && p.Offset == e.Offset)).ToList();

Console.WriteLine($"Total passages: {passages.Count}, to upsert: {toUpsert.Count}, to delete: {toDelete.Count}");

// call search client (mock or real) to upsert/delete
if (toUpsert.Any()) await search.UpsertAsync(toUpsert);
if (toDelete.Any()) await search.DeleteAsync(toDelete.Select(d => d.Id));

File.WriteAllText(outPath, JsonSerializer.Serialize(passages, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Wrote {passages.Count} passages to {outPath}");

// --- helpers ---

static List<string> ChunkText(string text, int approxSize)
{
    var parts = new List<string>();
    for (int i = 0; i < text.Length; i += approxSize)
    {
        int len = Math.Min(approxSize, text.Length - i);
        parts.Add(text.Substring(i, len));
    }
    return parts;
}

static string GenerateId(string file, int index, string chunk)
{
    using var sha = SHA1.Create();
    var input = Encoding.UTF8.GetBytes(file + index + chunk);
    var hash = sha.ComputeHash(input);
    return string.Concat(hash.Select(b => b.ToString("x2")));
}

static string ComputeHash(string s)
{
    using var sha = SHA256.Create();
    var h = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
    return string.Concat(h.Select(b => b.ToString("x2")));
}