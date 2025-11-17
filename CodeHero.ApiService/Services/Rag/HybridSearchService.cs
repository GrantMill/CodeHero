using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using CodeHero.ApiService.Contracts;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CodeHero.ApiService.Services.Rag;

/// <summary>
/// Provides hybrid search capabilities combining vector embeddings with keyword search.
/// </summary>
/// <remarks>
/// XML documentation below encodes guidance derived from two instruction sets used by the development tooling:
///
/// 1) Azure MCP Server usage rules (summary):
///    - Use Azure Tools: For Azure-related requests, use the provided Azure tools.
///    - Use Azure Code Gen Best Practices: When generating Azure code or running related commands, invoke the
///      best-practices tool if available; only call it when the user is discussing Azure.
///    - Use Azure Deployment Best Practices: When preparing for or performing Azure deployments, invoke the
///      best-practices tool if available; only call it when the user is discussing deployments.
///    - Use Azure Functions Code Gen Best Practices: When generating Azure Functions code or performing operations
///      related to Azure Functions, invoke the best-practices tool if available; only call it when relevant.
///    - Use Azure Functions Deployment Best Practices: When deploying Azure Functions, invoke the best-practices tool
///      if available; only call it when relevant.
///    - Use Azure SWA Best Practices: For Static Web Apps, invoke the best-practices tool when the user is discussing SWA.
///    - Plan before editing files for Azure web apps: When generating code for Azure Functions or Azure Static Web Apps,
///      always create a plan and explain it to the user before editing files, and only proceed after consent.
///    - Configuring the MCP coding agent: When asked about configuring or installing the Azure MCP coding agent for the
///      current repository, invoke the best-practices tool with resource="coding-agent" and action="all".
///
///    These rules are guidance for tool usage and deployment automation; they do not change runtime behavior of this class.
///
/// 2) Microsoft Learn MCP Server guidance (summary):
///    - Tools available:
///       * `microsoft_docs_search` - Search official Microsoft docs and return concise content chunks (use first for grounding).
///       * `microsoft_code_sample_search` - Search official docs for code examples (use when providing Microsoft/Azure code samples).
///       * `microsoft_docs_fetch` - Fetch full Microsoft documentation pages as markdown (use after search when full content needed).
///    - Workflow recommended:
///       1. Use `microsoft_docs_search` to find relevant docs.
///       2. Use `microsoft_code_sample_search` for practical code examples when needed.
///       3. Use `microsoft_docs_fetch` for detailed procedures, prerequisites, or complete tutorials.
///
///    Follow this workflow when grounding implementation details or providing Azure/Microsoft documentation references.
///
/// Note: This class implements runtime search behaviors and calls into Azure services. The above guidance is
/// documentation for contributors and for automated tooling that integrates with this repository; it is not executed
/// at runtime.
/// </remarks>
public sealed class HybridSearchService : IHybridSearchService
{
    private readonly SearchClient? _searchClient;
    private readonly System.Func<SearchRequest, System.Threading.CancellationToken, System.Threading.Tasks.Task<SearchResponse>>? _searchFunc;
    private readonly IEmbeddingProvider _embedder;
    private readonly ILogger<HybridSearchService> _log;
    private readonly string _vectorField = "contentVector";
    private readonly string _textField = "content";
    private readonly string _sourceMeta = "path";

    /// <summary>
    /// Initializes a new <see cref="HybridSearchService"/> with required dependencies.
    /// </summary>
    /// <param name="searchClient">Azure AI Search client targeting the configured index.</param>
    /// <param name="http">HTTP client factory used for embedding calls.</param>
    /// <param name="cfg">Configuration for embedding model and Foundry endpoint.</param>
    /// <param name="log">Logger for diagnostics/telemetry.</param>
    public HybridSearchService(SearchClient searchClient, IEmbeddingProvider embedder, ILogger<HybridSearchService> log)
    {
        _searchClient = searchClient;
        _embedder = embedder;
        _log = log;
    }

    // Internal constructor used by tests to provide a fake search function without requiring a real SearchClient.
    internal HybridSearchService(System.Func<SearchRequest, System.Threading.CancellationToken, System.Threading.Tasks.Task<SearchResponse>> searchFunc, IEmbeddingProvider embedder, ILogger<HybridSearchService> log)
    {
        _searchFunc = searchFunc;
        _embedder = embedder;
        _log = log;
    }

    /// <summary>
    /// Executes a hybrid search using an embedded vector for semantic similarity and keyword fallback.
    /// </summary>
    /// <param name="req">The search request containing the query and options such as TopK.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="SearchResponse"/> containing scored hits.</returns>
    public async Task<SearchResponse> SearchAsync(SearchRequest req, CancellationToken ct = default)
    {
        var vector = await _embedder.GetEmbeddingAsync(req.StandaloneQuestion, ct) ?? Array.Empty<float>();

        // If embedding is unavailable or empty, short-circuit and return an empty result set.
        if (vector.Length == 0)
        {
            return new SearchResponse(new List<SearchHit>());
        }

        var options = new SearchOptions
        {
            Size = req.TopK,
            QueryType = SearchQueryType.Simple // fallback to keyword + vector; remove semantic specifics for SDK compatibility
        };
        options.VectorSearch = new()
        {
            Queries = { new VectorizedQuery(vector) { KNearestNeighborsCount = req.TopK, Fields = { _vectorField } } }
        };
        options.Select.Add(_textField);
        options.Select.Add(_sourceMeta);

        // If a test-provided search function exists, use it to obtain deterministic results.
        if (_searchFunc is not null)
        {
            return await _searchFunc(req, ct);
        }

        var response = await _searchClient.SearchAsync<SearchDocument>(req.StandaloneQuestion, options, ct);
        var hits = new List<SearchHit>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            var doc = result.Document;
            string content = doc.GetString(_textField) ?? string.Empty;
            string source = ExtractSource(doc);
            hits.Add(new SearchHit(content, source, result.Score ?? 0));
        }
        return new SearchResponse(hits);
    }

    /// <summary>
    /// Calls the configured Azure OpenAI embeddings endpoint to produce a vector for the provided text.
    /// </summary>
    /// <param name="text">Text to embed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An array of floats representing the embedding, or null on failure or missing configuration.</returns>
    // Embedding is delegated to IEmbeddingProvider to allow pluggable providers and easier testing.

    /// <summary>
    /// Extracts a source URL or path string from a <see cref="SearchDocument"/> using resilient parsing of common shapes.
    /// </summary>
    /// <param name="doc">The search document to extract metadata from.</param>
    /// <returns>A string containing the extracted source or an empty string if unavailable.</returns>
    private string ExtractSource(SearchDocument doc)
    {
        // Try the retrievable 'path' field first (matches current index), accept string or object shapes.
        if (doc.TryGetValue(_sourceMeta, out var pathObj))
        {
            if (pathObj is string s && !string.IsNullOrEmpty(s))
                return s;

            if (pathObj is IDictionary<string, object> pd && pd.TryGetValue("url", out var urlObj))
                return urlObj?.ToString() ?? string.Empty;
        }

        // Fall back to resilient parsing of a 'metadata' object if present in documents.
        if (doc.TryGetValue("metadata", out var metaObj) && metaObj is IDictionary<string, object> md)
        {
            if (md.TryGetValue("source", out var srcObj))
            {
                if (srcObj is string srcStr && !string.IsNullOrEmpty(srcStr))
                    return srcStr;

                if (srcObj is IDictionary<string, object> srcDict && srcDict.TryGetValue("url", out var urlObj))
                    return urlObj?.ToString() ?? string.Empty;
            }

            if (md.TryGetValue("url", out var directUrl))
                return directUrl?.ToString() ?? string.Empty;
        }

        return string.Empty;
    }
}