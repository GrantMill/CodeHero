using CodeHero.Services;
using Microsoft.AspNetCore.Mvc;

namespace CodeHero.Controllers;

[ApiController]
[Route("api/search/indexer")]
public class SearchIndexerController : ControllerBase
{
    private readonly ISearchIndexerService _indexer;
    private readonly ILogger<SearchIndexerController> _logger;

    public SearchIndexerController(ISearchIndexerService indexer, ILogger<SearchIndexerController> logger)
    {
        _indexer = indexer;
        _logger = logger;
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken cancellationToken)
    {
        var res = await _indexer.CreateIndexAndRunAsync(cancellationToken);
        if (res.Success) return Ok(res);
        return StatusCode(500, res);
    }

    [HttpGet("history")]
    public async Task<IActionResult> History()
    {
        var h = await _indexer.GetHistoryAsync();
        return Ok(h);
    }
}