using CodeHero.Indexer.Interfaces;
using CodeHero.Indexer.Models;

namespace CodeHero.Indexer.Clients;

public class MockSearchClient : ISearchClient
{
 public System.Threading.Tasks.Task UpsertAsync(IEnumerable<Passage> docs)
 {
 Console.WriteLine($"[MockSearch] Upsert {docs.Count()} docs");
 return System.Threading.Tasks.Task.CompletedTask;
 }

 public System.Threading.Tasks.Task DeleteAsync(IEnumerable<string> ids)
 {
 Console.WriteLine($"[MockSearch] Delete {ids.Count()} docs");
 return System.Threading.Tasks.Task.CompletedTask;
 }
}