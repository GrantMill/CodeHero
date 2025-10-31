using CodeHero.Indexer.Interfaces;

namespace CodeHero.Indexer.Clients;

public class MockEmbeddingClient : IEmbeddingClient
{
 public float[] Embed(string s)
 {
 var v = new float[128];
 foreach (var c in s)
 {
 v[c %128] +=1;
 }
 var norm = (float)Math.Sqrt(v.Sum(x => x * x));
 if (norm >0)
 for (int i =0; i < v.Length; i++) v[i] /= norm;
 return v;
 }
}