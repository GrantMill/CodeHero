using CodeHero.Models;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CodeHero.Services;

public interface IBackgroundIndexer
{
    Task<Guid> TriggerIndexingAsync(CancellationToken ct = default);

    Task<IndexerRunResult?> GetStatusAsync(Guid jobId);
}

public sealed class BackgroundIndexerService : BackgroundService, IBackgroundIndexer
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly ConcurrentDictionary<Guid, IndexerRunResult?> _jobs = new();
    private readonly IServiceProvider _sp;
    private readonly ILogger<BackgroundIndexerService> _log;

    public BackgroundIndexerService(IServiceProvider sp, ILogger<BackgroundIndexerService> log)
    {
        _sp = sp ?? throw new ArgumentNullException(nameof(sp));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<Guid> TriggerIndexingAsync(CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        _jobs[id] = null; // queued
        await _queue.Writer.WriteAsync(id, ct).ConfigureAwait(false);
        _log.LogInformation("Enqueued index job {JobId}", id);
        return id;
    }

    public Task<IndexerRunResult?> GetStatusAsync(Guid jobId)
    {
        _jobs.TryGetValue(jobId, out var res);
        return Task.FromResult(res);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _queue.Reader;
        try
        {
            while (await reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
            {
                while (reader.TryRead(out var jobId))
                {
                    _log.LogInformation("Starting index job {JobId}", jobId);
                    try
                    {
                        // create a scope to get the transient indexer service
                        using var scope = _sp.CreateScope();
                        var indexer = scope.ServiceProvider.GetRequiredService<ISearchIndexerService>();

                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        // optional per-job timeout from config could be used here
                        var res = await indexer.CreateIndexAndRunAsync(cts.Token).ConfigureAwait(false);
                        _jobs[jobId] = res;
                        _log.LogInformation("Index job {JobId} completed: Success={Success}", jobId, res.Success);
                    }
                    catch (Exception exJob)
                    {
                        var failure = new IndexerRunResult { Id = Guid.NewGuid(), Timestamp = DateTimeOffset.UtcNow, Success = false, Message = "Job failed", ErrorDetails = exJob.ToString() };
                        _jobs[jobId] = failure;
                        _log.LogError(exJob, "Index job {JobId} failed", jobId);
                    }
                }
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }
}