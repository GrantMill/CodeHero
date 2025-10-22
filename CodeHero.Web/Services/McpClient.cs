using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CodeHero.Web.Services;

public sealed class McpClient : IMcpClient
{
    private readonly Process _proc;
    private readonly Stream _in;
    private readonly Stream _out;

    public McpClient()
    {
        // Locate server next to the web build for dev simplicity
        var baseDir = AppContext.BaseDirectory;
        var serverDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "CodeHero.McpServer", "bin", "Debug", "net10.0"));
        var dll = Path.Combine(serverDir, "CodeHero.McpServer.dll");
        var psi = new ProcessStartInfo("dotnet", $"\"{dll}\"")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = serverDir,
        };
        // Configure content roots for the server so fs/* and scribe tools work when launched from the web app
        var repoRoot = Path.GetFullPath(Path.Combine(serverDir, "..", "..", "..", ".."));
        psi.Environment["ContentRoots__Requirements"] = Path.Combine(repoRoot, "docs", "requirements");
        psi.Environment["ContentRoots__Architecture"] = Path.Combine(repoRoot, "docs", "architecture");
        psi.Environment["ContentRoots__Features"] = Path.Combine(repoRoot, "docs", "features");
        psi.Environment["ContentRoots__Artifacts"] = Path.Combine(repoRoot, "artifacts");
        // Backlog is required by FileStore
        psi.Environment["ContentRoots__Backlog"] = Path.Combine(repoRoot, "plan", "BACKLOG.md");
        // Legacy/optional plans retained for forward compatibility
        psi.Environment["ContentRoots__HumanPlan"] = Path.Combine(repoRoot, "plan", "HUMAN.md");
        psi.Environment["ContentRoots__AgentPlan"] = Path.Combine(repoRoot, "plan", "AGENT.md");
        _proc = Process.Start(psi)!;
        _in = _proc.StandardInput.BaseStream;
        _out = _proc.StandardOutput.BaseStream;
    }

    private static async Task SendAsync(Stream s, object req, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(req);
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        await s.WriteAsync(header, ct);
        await s.WriteAsync(bytes, ct);
        await s.FlushAsync(ct);
    }

    private static async Task<string> ReadAsync(Stream s, CancellationToken ct)
    {
        var header = new MemoryStream();
        var last4 = new byte[4];
        var one = new byte[1];
        int count = 0;
        while (true)
        {
            int r = await s.ReadAsync(one.AsMemory(0,1), ct);
            if (r == 0) throw new EndOfStreamException();
            header.Write(one, 0, 1);
            last4[count % 4] = one[0];
            count++;
            if (count >= 4 && last4[(count - 4) % 4] == (byte)'\r' && last4[(count - 3) % 4] == (byte)'\n' && last4[(count - 2) % 4] == (byte)'\r' && last4[(count - 1) % 4] == (byte)'\n') break;
        }
        var headerText = Encoding.ASCII.GetString(header.ToArray());
        var lenLine = headerText.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                                .First(l => l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
        var len = int.Parse(lenLine.Split(':', 2)[1].Trim());
        var buf = ArrayPool<byte>.Shared.Rent(len);
        try
        {
            int read = 0;
            while (read < len)
            {
                int r = await s.ReadAsync(buf.AsMemory(read, len - read), ct);
                if (r == 0) throw new EndOfStreamException();
                read += r;
            }
            return Encoding.UTF8.GetString(buf, 0, len);
        }
        finally { ArrayPool<byte>.Shared.Return(buf); }
    }

    public async Task<bool> InitializeAsync(CancellationToken ct = default)
    {
        await SendAsync(_in, new { jsonrpc = "2.0", method = "mcp/initialize", id = "1" }, ct);
        var resp = await ReadAsync(_out, ct);
        return resp.Contains("\"jsonrpc\":\"2.0\"");
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        await SendAsync(_in, new { jsonrpc = "2.0", method = "ping", id = "2" }, ct);
        var resp = await ReadAsync(_out, ct);
        return resp.Contains("\"pong\"");
    }

    public async Task<IReadOnlyList<string>> ListAsync(StoreRoot root, string[]? exts = null, CancellationToken ct = default)
    {
        await SendAsync(_in, new { jsonrpc = "2.0", method = "fs/list", id = "3", @params = new { root = root.ToString().ToLowerInvariant(), exts = exts ?? Array.Empty<string>() } }, ct);
        var resp = await ReadAsync(_out, ct);
        using var doc = JsonDocument.Parse(resp);
        var arr = doc.RootElement.GetProperty("result").GetProperty("files");
        return arr.EnumerateArray().Select(e => e.GetString()!).ToArray();
    }

    public async Task<string> ReadTextAsync(StoreRoot root, string name, string[]? exts = null, CancellationToken ct = default)
    {
        await SendAsync(_in, new { jsonrpc = "2.0", method = "fs/readText", id = "4", @params = new { root = root.ToString().ToLowerInvariant(), name, exts = exts ?? Array.Empty<string>() } }, ct);
        var resp = await ReadAsync(_out, ct);
        using var doc = JsonDocument.Parse(resp);
        return doc.RootElement.GetProperty("result").GetProperty("text").GetString() ?? string.Empty;
    }

    public async Task WriteTextAsync(StoreRoot root, string name, string content, string[]? exts = null, CancellationToken ct = default)
    {
        await SendAsync(_in, new { jsonrpc = "2.0", method = "fs/writeText", id = "5", @params = new { root = root.ToString().ToLowerInvariant(), name, content, exts = exts ?? Array.Empty<string>() } }, ct);
        _ = await ReadAsync(_out, ct);
    }

    public async Task<IReadOnlyList<string>> ListAgentsAsync(CancellationToken ct = default)
    {
        await SendAsync(_in, new { jsonrpc = "2.0", method = "agents/list", id = "6" }, ct);
        var resp = await ReadAsync(_out, ct);
        using var doc = JsonDocument.Parse(resp);
        var arr = doc.RootElement.GetProperty("result").GetProperty("agents");
        return arr.EnumerateArray().Select(e => e.GetString()!).ToArray();
    }

    public async Task<JsonElement> GetAgentCapabilitiesAsync(string agent, CancellationToken ct = default)
    {
        await SendAsync(_in, new { jsonrpc = "2.0", method = "agents/capabilities", id = "7", @params = new { agent } }, ct);
        var resp = await ReadAsync(_out, ct);
        using var doc = JsonDocument.Parse(resp);
        // Clone to detach from disposed JsonDocument
        return doc.RootElement.GetProperty("result").Clone();
    }

    public async Task<string> ScribeCreateRequirementAsync(string id, string title, CancellationToken ct = default)
    {
        await SendAsync(_in, new { jsonrpc = "2.0", method = "scribe/createRequirement", id = "8", @params = new { id, title } }, ct);
        var resp = await ReadAsync(_out, ct);
        using var doc = JsonDocument.Parse(resp);
        return doc.RootElement.GetProperty("result").GetProperty("created").GetString() ?? string.Empty;
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        try
        {
            await SendAsync(_in, new { jsonrpc = "2.0", method = "mcp/shutdown", id = "6" }, ct);
            _ = await ReadAsync(_out, ct);
        }
        catch { }
        try { if (!_proc.HasExited) _proc.Kill(true); } catch { }
    }

    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync();
        _in.Dispose();
        _out.Dispose();
        _proc.Dispose();
    }
}
