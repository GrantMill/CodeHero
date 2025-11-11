namespace CodeHero.Models;

public class IndexerRunResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int DocumentsIndexed { get; set; }
    public string? ErrorDetails { get; set; }
}