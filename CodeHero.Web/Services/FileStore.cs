using System.Text;

namespace CodeHero.Web.Services;

public enum StoreRoot { Requirements, Architecture, Artifacts }

public sealed class FileStore
{
    private readonly IWebHostEnvironment _env;
    private readonly Dictionary<StoreRoot, string> _roots;
    private readonly string _humanPlanPath;
    private readonly string _agentPlanPath;

    public FileStore(IConfiguration config, IWebHostEnvironment env)
    {
        _env = env;
        _roots = new()
        {
            [StoreRoot.Requirements] = Resolve(config, "ContentRoots:Requirements"),
            [StoreRoot.Architecture] = Resolve(config, "ContentRoots:Architecture"),
            [StoreRoot.Artifacts] = Resolve(config, "ContentRoots:Artifacts"),
        };

        // Optional single-file roots for plan documents
        _humanPlanPath = Resolve(config, "ContentRoots:HumanPlan");
        _agentPlanPath = Resolve(config, "ContentRoots:AgentPlan");
    }

    private static string Normalize(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private string Resolve(IConfiguration config, string key)
    {
        var rel = config[key] ?? throw new InvalidOperationException($"Missing configuration: {key}");
        var combined = Path.Combine(_env.ContentRootPath, rel);
        return Normalize(combined);
    }

    private string Guard(StoreRoot root, string name, params string[] allowedExts)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Filename required", nameof(name));
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains(Path.DirectorySeparatorChar) || name.Contains(Path.AltDirectorySeparatorChar))
            throw new InvalidOperationException("Invalid filename");

        var full = Normalize(Path.Combine(_roots[root], name));
        if (!full.StartsWith(_roots[root], StringComparison.Ordinal))
            throw new InvalidOperationException("Path traversal blocked");

        if (allowedExts is { Length: > 0 } && allowedExts.All(ext => !name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Extension not allowed");

        return full;
    }

    public IEnumerable<string> List(StoreRoot root, params string[] exts)
    {
        var dir = _roots[root];
        if (!Directory.Exists(dir)) return Enumerable.Empty<string>();
        var files = Directory.EnumerateFiles(dir);
        if (exts is { Length: > 0 })
            files = files.Where(f => exts.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase)));
        return files.Select(Path.GetFileName)!;
    }

    public string ReadText(StoreRoot root, string name, params string[] exts)
        => File.ReadAllText(Guard(root, name, exts));

    public void WriteText(StoreRoot root, string name, string content, params string[] exts)
        => File.WriteAllText(Guard(root, name, exts), content ?? string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

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

    public string ReadHumanPlan() => File.Exists(_humanPlanPath) ? File.ReadAllText(_humanPlanPath) : string.Empty;
    public string ReadAgentPlan() => File.Exists(_agentPlanPath) ? File.ReadAllText(_agentPlanPath) : string.Empty;
    public void WriteHumanPlan(string content) => File.WriteAllText(_humanPlanPath, content ?? string.Empty, new UTF8Encoding(false));
    public void WriteAgentPlan(string content) => File.WriteAllText(_agentPlanPath, content ?? string.Empty, new UTF8Encoding(false));
}
