namespace CodeHero.Indexer.Models;

public record Passage
{
 public string Id { get; init; } = string.Empty;
 public string Text { get; init; } = string.Empty;
 public string Source { get; init; } = string.Empty;
 public int Offset { get; init; }
 public string Hash { get; init; } = string.Empty;
 public float[] Vector { get; init; } = Array.Empty<float>();
}