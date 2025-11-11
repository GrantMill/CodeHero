namespace CodeHero.Indexer.Interfaces;

using CodeHero.Indexer.Models;

public interface ISearchClient
{
    System.Threading.Tasks.Task UpsertAsync(IEnumerable<Passage> docs);

    System.Threading.Tasks.Task DeleteAsync(IEnumerable<string> ids);
}