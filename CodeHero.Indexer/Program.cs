using System.Text.Json;
using System.Security.Cryptography;

Console.WriteLine("CodeHero Indexer - Phase2 (local mock + pluggable clients)");

var root = Directory.GetCurrentDirectory();
var repoRoot = Path.GetFullPath(Path.Combine(root, ".."));
var docsDir = Path.Combine(repoRoot, "docs");
var outputDir = Path.Combine(repoRoot, "data");
Directory.CreateDirectory(outputDir);

var files = Directory.Exists(docsDir)
 ? Directory.GetFiles(docsDir, "*.*", SearchOption.AllDirectories)
 .Where(f => f.EndsWith(".md") || f.EndsWith(".mmd") || f.EndsWith(".txt") || f.EndsWith(".cs"))
 .ToArray()
 : Array.Empty<string>();

Console.WriteLine($"Found {files.Length} files under {docsDir}");

var passages = new List<object>();

foreach (var file in files)
{
 var text = File.ReadAllText(file);
 var chunks = ChunkText(text,1000);
 for (int i =0; i < chunks.Count; i++)
 {
 var chunk = chunks[i];
 var id = GenerateId(file, i, chunk);
 var vector = MockEmbedding(chunk);
 var hash = ComputeHash(chunk);
 passages.Add(new {
 id,
 text = chunk,
 source = Path.GetRelativePath(repoRoot, file).Replace("\\","/"),
 offset = i,
 hash,
 vector
 });
 }
}

var outPath = Path.Combine(outputDir, "indexed.json");
File.WriteAllText(outPath, JsonSerializer.Serialize(passages, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Wrote {passages.Count} passages to {outPath}");

static List<string> ChunkText(string text, int approxSize)
{
 var parts = new List<string>();
 for (int i =0; i < text.Length; i += approxSize)
 {
 int len = Math.Min(approxSize, text.Length - i);
 parts.Add(text.Substring(i, len));
 }
 return parts;
}

static string GenerateId(string file, int index, string chunk)
{
 using var sha = SHA1.Create();
 var input = System.Text.Encoding.UTF8.GetBytes(file + index + chunk);
 var hash = sha.ComputeHash(input);
 return string.Concat(hash.Select(b => b.ToString("x2")));
}

static float[] MockEmbedding(string s)
{
 // deterministic pseudo-embedding: histogram of char codes mod100
 var v = new float[128];
 foreach (var c in s)
 {
 v[c %128] +=1;
 }
 // normalize
 var norm = (float)Math.Sqrt(v.Sum(x => x * x));
 if (norm >0)
 for (int i =0; i < v.Length; i++) v[i] /= norm;
 return v;
}

static string ComputeHash(string s)
{
 using var sha = SHA256.Create();
 var h = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s));
 return string.Concat(h.Select(b => b.ToString("x2")));
}