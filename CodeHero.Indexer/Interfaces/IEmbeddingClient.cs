namespace CodeHero.Indexer.Interfaces;

public interface IEmbeddingClient
{
    System.Threading.Tasks.Task<float[]> EmbedAsync(string text);
}