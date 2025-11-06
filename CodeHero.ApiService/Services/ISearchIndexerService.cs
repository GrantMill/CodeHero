using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeHero.Models;

namespace CodeHero.Services;

public interface ISearchIndexerService
{
    Task<IndexerRunResult> CreateIndexAndRunAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<IndexerRunResult>> GetHistoryAsync(int max = 50);
}