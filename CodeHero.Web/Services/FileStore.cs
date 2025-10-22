using System.Text;

namespace CodeHero.Web.Services;

public enum StoreRoot { Requirements, Architecture, Artifacts }

public sealed class FileStore
{
    private readonly IWebHostEnvironment _env;
    private readonly Dictionary<StoreRoot, string> _roots;
    private readonly string _backlogPath;
    private readonly string? _roadmapPath;

    public FileStore(IConfiguration config, IWebHostEnvironment env)
    {
        _env = env;
        _roots = new()
        {
            [StoreRoot.Requirements] = Resolve(config, "ContentRoots:Requirements"),
            [StoreRoot.Architecture] = Resolve(config, "ContentRoots:Architecture"),
            [StoreRoot.Artifacts] = Resolve(config, "ContentRoots:Artifacts"),
        };

        // Single-file roots
        _backlogPath = Resolve(config, "ContentRoots:Backlog");
        // Roadmap is optional; guard missing key
        _roadmapPath = TryResolve(config, "ContentRoots:Roadmap");
    }

    private static string Normalize(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private string Resolve(IConfiguration config, string key)
    {
        var rel = config[key] ?? throw new InvalidOperationException($"Missing configuration: {key}");
        var combined = Path.Combine(_env.ContentRootPath, rel);
        return Normalize(combined);
    }

    private string? TryResolve(IConfiguration config, string key)
    {
        var rel = config[key];
        if (string.IsNullOrWhiteSpace(rel)) return null;
        var combined = Path.Combine(_env.ContentRootPath, rel);
        return Normalize(combined);
    }

    private string Guard(StoreRoot root, string name, params string[] allowedExts)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Filename required", nameof(name));
        // Cross-platform guard: reject directory separators or traversal tokens
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >=0 || name.Contains('/') || name.Contains('\\'))
            throw new InvalidOperationException("Invalid filename");
        if (name.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid filename");

        var full = Normalize(Path.Combine(_roots[root], name));
        if (!full.StartsWith(_roots[root], StringComparison.Ordinal))
            throw new InvalidOperationException("Path traversal blocked");

        if (allowedExts is { Length: >0 } && allowedExts.All(ext => !name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Extension not allowed");

        return full;
    }

    public IEnumerable<string> List(StoreRoot root, params string[] exts)
    {
        var dir = _roots[root];
        if (!Directory.Exists(dir)) return Enumerable.Empty<string>();
        var files = Directory.EnumerateFiles(dir);
        if (exts is { Length: >0 })
            files = files.Where(f => exts.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase)));
        return files.Select(Path.GetFileName)!;
    }

    public string ReadText(StoreRoot root, string name, params string[] exts)
        => File.ReadAllText(Guard(root, name, exts));

    public void WriteText(StoreRoot root, string name, string content, params string[] exts)
    {
        var full = Guard(root, name, exts);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(full, content ?? string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public (string SavedPath, string MetaPath) SaveArtifact(Stream file, string filename, string? note)
    {
        var ts = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var safe = Guard(StoreRoot.Artifacts, $"{ts}-{filename}");
        using (var fs = File.Create(safe))
        {
            file.CopyTo(fs);
        }
        var meta = safe + ".yml";
        File.WriteAllText(meta, $"uploaded: {DateTime.UtcNow:o}\nname: {filename}\nnote: \"{(note ?? string.Empty).Replace("\"", "''")}\"\n");
        return (safe, meta);
    }

    // Unified backlog helpers
    public string ReadBacklog() => File.Exists(_backlogPath) ? File.ReadAllText(_backlogPath) : string.Empty;
    public void WriteBacklog(string content)
        => File.WriteAllText(_backlogPath, content ?? string.Empty, new UTF8Encoding(false));

    // Roadmap helper (optional)
    public string ReadRoadmap()
        => !string.IsNullOrWhiteSpace(_roadmapPath) && File.Exists(_roadmapPath) ? File.ReadAllText(_roadmapPath) : string.Empty;

    // Helper to read arbitrary relative file under solution root if needed
    public string TryReadRelative(string relativePath, params string[] exts)
    {
        var full = Normalize(Path.Combine(_env.ContentRootPath, relativePath));
        if (!File.Exists(full)) return string.Empty;
        if (exts is { Length: >0 } && exts.All(e => !full.EndsWith(e, StringComparison.OrdinalIgnoreCase))) return string.Empty;
        return File.ReadAllText(full);
    }
}
