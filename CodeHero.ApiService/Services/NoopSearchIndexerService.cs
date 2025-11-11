using CodeHero.Models;
using System.Collections.Concurrent;

namespace CodeHero.Services;

public class NoopSearchIndexerService : ISearchIndexerService
{
    private readonly ILogger<NoopSearchIndexerService> _logger;
    private readonly ConcurrentQueue<IndexerRunResult> _history = new();

    public NoopSearchIndexerService(ILogger<NoopSearchIndexerService> logger, IConfiguration config)
    {
        _logger = logger;
    }

    public Task<IEnumerable<IndexerRunResult>> GetHistoryAsync(int max = 50)
    {
        var arr = _history.ToArray();
        return Task.FromResult<IEnumerable<IndexerRunResult>>(arr);
    }

    public Task<IndexerRunResult> CreateIndexAndRunAsync(CancellationToken cancellationToken = default)
    {
        var r = new IndexerRunResult
        {
            Success = false,
            Message = "Search not configured. Noop indexer did nothing."
        };
        _history.Enqueue(r);
        return Task.FromResult(r);
    }
}