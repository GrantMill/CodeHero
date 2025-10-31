namespace CodeHero.Indexer.Interfaces;

public interface IEmbeddingClient
{
 float[] Embed(string text);
}