using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using CodeHero.Models;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeHero.Services;

public class AzureSearchIndexerService : ISearchIndexerService
{
    private readonly IConfiguration _config;
    private readonly ILogger<AzureSearchIndexerService> _logger;
    private readonly string? _endpoint;
    private readonly string? _apiKey;
    private readonly string _indexName;
    private readonly string _contentRoot;
    private readonly ConcurrentQueue<IndexerRunResult> _history = new();
    private readonly IAzureSearchClientFactory _clientFactory;

    public AzureSearchIndexerService(IConfiguration config, ILogger<AzureSearchIndexerService> logger, IAzureSearchClientFactory clientFactory)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _endpoint = _config["Search:Endpoint"] ?? _config["AzureSearch:Endpoint"];
        _apiKey = _config["Search:ApiKey"] ?? _config["AzureSearch:ApiKey"];
        _indexName = string.IsNullOrWhiteSpace(_config["Search:IndexName"]) && string.IsNullOrWhiteSpace(_config["AzureSearch:IndexName"]) ? "codehero-docs" : (_config["Search:IndexName"] ?? _config["AzureSearch:IndexName"]!);
        _contentRoot = Path.GetFullPath(_config["ContentRoot"] ?? Directory.GetCurrentDirectory());
    }

    private bool IsConfigured => !string.IsNullOrWhiteSpace(_endpoint) && !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<IndexerRunResult> CreateIndexAndRunAsync(CancellationToken cancellationToken = default)
    {
        var result = new IndexerRunResult();
        if (!IsConfigured)
        {
            result.Success = false;
            result.Message = "AzureSearch is not configured (Search:Endpoint or Search:ApiKey missing).";
            Enqueue(result);
            return result;
        }

        try
        {
            // create index if missing
            await EnsureIndexExistsAsync(cancellationToken).ConfigureAwait(false);

            // scan repo files
            var files = ScanRepositoryFiles(_contentRoot);
            _logger.LogInformation("Files discovered for indexing: {Count}", files.Count);

            const int batchSize = 200;
            int totalIndexed = 0;

            var endpointUri = new Uri(_endpoint!);
            var keyCredential = new AzureKeyCredential(_apiKey!);

            var documentMap = new List<object>();

            foreach (var batch in Partition(files, batchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var docs = new List<Dictionary<string, object>>(batch.Count);
                foreach (var file in batch)
                {
                    try
                    {
                        string content = await ReadFileContentAsync(file, cancellationToken).ConfigureAwait(false);

                        var relativePath = Path.GetRelativePath(_contentRoot, file).Replace('\\', '/');

                        var id = GenerateId(relativePath);

                        var doc = new Dictionary<string, object>
                        {
                            ["id"] = id,
                            ["path"] = relativePath,
                            ["content"] = content,
                            ["contentType"] = Path.GetExtension(file).TrimStart('.'),
                            ["lastModified"] = File.GetLastWriteTimeUtc(file)
                        };
                        docs.Add(doc);

                        // build document map entry for agent/context
                        var title = ExtractTitle(content);
                        var headings = ExtractHeadings(content);
                        var links = ExtractLinks(content);
                        var tags = ExtractTags(content);
                        var size = new FileInfo(file).Length;
                        var contentHash = ComputeContentHash(content);

                        documentMap.Add(new
                        {
                            id,
                            relativePath,
                            title,
                            headings,
                            links,
                            tags,
                            lastModified = File.GetLastWriteTimeUtc(file),
                            size,
                            contentHash
                        });
                    }
                    catch (Exception exFile)
                    {
                        _logger.LogWarning(exFile, "Skipping file because of error: {File}", file);
                    }
                }

                if (docs.Count == 0) continue;

                var searchClient = _clientFactory.CreateSearchClient(endpointUri, _indexName, keyCredential);
                var batchUpload = IndexDocumentsBatch.Upload(docs);
                var response = await searchClient.IndexDocumentsAsync(batchUpload, cancellationToken: cancellationToken).ConfigureAwait(false);

                // response might not surface success per-doc; we count intended docs
                totalIndexed += docs.Count;
            }

            // write document map to data/document-map.json under content root
            try
            {
                var dataDir = Path.Combine(_contentRoot, "data");
                Directory.CreateDirectory(dataDir);
                var mapPath = Path.Combine(dataDir, "document-map.json");
                var opts = new JsonSerializerOptions { WriteIndented = true };
                await File.WriteAllTextAsync(mapPath, JsonSerializer.Serialize(documentMap, opts), cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Wrote document map to {Path}", mapPath);
            }
            catch (Exception exMap)
            {
                _logger.LogWarning(exMap, "Failed to write document map");
            }

            result.Success = true;
            result.Message = $"Indexed {totalIndexed} documents into index '{_indexName}'.";
            result.DocumentsIndexed = totalIndexed;
            Enqueue(result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Indexing failed");
            result.Success = false;
            result.Message = "Indexing failed";
            result.ErrorDetails = ex.ToString();
            Enqueue(result);
            return result;
        }
    }

    public Task<IEnumerable<IndexerRunResult>> GetHistoryAsync(int max = 50)
    {
        var arr = _history.ToArray().OrderByDescending(r => r.Timestamp).Take(max);
        return Task.FromResult<IEnumerable<IndexerRunResult>>(arr);
    }

    private void Enqueue(IndexerRunResult r)
    {
        _history.Enqueue(r);
        // keep history small
        while (_history.Count > 200 && _history.TryDequeue(out _)) { }
    }

    private async Task EnsureIndexExistsAsync(CancellationToken cancellationToken)
    {
        var endpointUri = new Uri(_endpoint!);
        var keyCredential = new AzureKeyCredential(_apiKey!);

        try
        {
            var indexClient = _clientFactory.CreateIndexClient(endpointUri, keyCredential);
            await indexClient.GetIndexAsync(_indexName, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Index '{IndexName}' already exists.", _indexName);
            return;
        }
        catch (RequestFailedException rfEx) when (rfEx.Status == 404)
        {
            // no-op, proceed to create
        }
        catch (Exception ex)
        {
            // Some APIs may throw on Get; try create anyway
            _logger.LogWarning(ex, "Get index existence check failed; will attempt to create index.");
        }

        var fields = new List<SearchField>
        {
            new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
            new SearchableField("content") { IsFilterable = false, IsSortable = false, IsFacetable = false },
            new SearchField("path", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = false, IsSortable = true },
            new SearchField("contentType", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
            new SimpleField("lastModified", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true }
        };

        var definition = new SearchIndex(_indexName, fields)
        {
            // Add suggesters or semantic config later if desired
        };

        var indexClientCreate = _clientFactory.CreateIndexClient(endpointUri, keyCredential);
        await indexClientCreate.CreateIndexAsync(definition, cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Index '{IndexName}' created.", _indexName);
    }

    private string GenerateId(string relativePath)
    {
        // stable id based on normalized relative path
        var normalized = relativePath.Replace('\\', '/').ToLowerInvariant();
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    private static string ComputeContentHash(string s)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes);
    }

    private static string ExtractTitle(string content)
    {
        // try YAML frontmatter
        if (content.StartsWith("---"))
        {
            var end = content.IndexOf("---", 3);
            if (end > 0)
            {
                var fm = content.Substring(3, end - 3);
                var m = Regex.Match(fm, @"^title:\s*(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (m.Success)
                    return m.Groups[1].Value.Trim().Trim('"', '\'');
            }
        }

        // fallback: first H1 or H2
        var h = Regex.Match(content, @"^#{1,2}\s+(.+)$", RegexOptions.Multiline);
        return h.Success ? h.Groups[1].Value.Trim() : string.Empty;
    }

    private static List<string> ExtractHeadings(string content)
    {
        var matches = Regex.Matches(content, @"^#{1,6}\s+(.+)$", RegexOptions.Multiline);
        var list = new List<string>(matches.Count);
        foreach (Match m in matches) list.Add(m.Groups[1].Value.Trim());
        return list;
    }

    private static List<string> ExtractLinks(string content)
    {
        var matches = Regex.Matches(content, @"\[[^\]]+\]\(([^)]+)\)");
        var list = new List<string>(matches.Count);
        foreach (Match m in matches) list.Add(m.Groups[1].Value.Trim());
        return list.Distinct().ToList();
    }

    private static List<string> ExtractTags(string content)
    {
        var matches = Regex.Matches(content, @"\bREQ-\d+\b", RegexOptions.IgnoreCase);
        var list = new List<string>(matches.Count);
        foreach (Match m in matches) list.Add(m.Value.ToUpperInvariant());
        return list.Distinct().ToList();
    }

    private List<string> ScanRepositoryFiles(string root)
    {
        var files = new List<string>();
        var excludeDirs = new[] { "bin", "obj", ".git", ".vs", "node_modules", "packages" };
        var allowedExtensions = new[]
        {
            ".cs", ".csproj", ".sln", ".md", ".mmd", ".txt", ".json", ".xml", ".config", ".html", ".razor", ".razor.cs",
            ".js", ".ts", ".css", ".yml", ".yaml", ".ps1", ".sh", ".dockerfile", ".mdown"
        };

        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            try
            {
                foreach (var d in Directory.EnumerateDirectories(dir))
                {
                    var name = Path.GetFileName(d);
                    if (string.IsNullOrEmpty(name)) continue;
                    if (excludeDirs.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                    stack.Push(d);
                }

                foreach (var f in Directory.EnumerateFiles(dir))
                {
                    var ext = Path.GetExtension(f);
                    if (allowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                    {
                        files.Add(f);
                    }
                }
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (DirectoryNotFoundException) { continue; }
        }

        return files;
    }

    private static async Task<string> ReadFileContentAsync(string file, CancellationToken cancellationToken)
    {
        // read text with fallback
        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = await sr.ReadToEndAsync().ConfigureAwait(false);
        return content;
    }

    private static IEnumerable<List<T>> Partition<T>(List<T> source, int size)
    {
        for (int i = 0; i < source.Count; i += size)
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
    }
}