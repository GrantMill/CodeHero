using System.Buffers;
using System.Text;
using System.Text.Json;
using CodeHero.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Minimal MCP-like stdio JSON-RPC server implementing initialize, ping, fs/*.
// JSON-RPC 2.0 over Content-Length framing to stdout/stderr.

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(b => b.ClearProviders())
    .ConfigureServices((ctx, services) =>
    {
        // Reuse FileStore and content roots from appsettings.
        services.AddSingleton<FileStore>();
    })
    .Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MCP");
var cfg = host.Services.GetRequiredService<IConfiguration>();
var store = host.Services.GetRequiredService<FileStore>();

logger.LogInformation("MCP server starting");

await RunAsync(store, logger);

static async Task RunAsync(FileStore store, ILogger logger)
{
    var stdin = Console.OpenStandardInput();
    var stdout = Console.OpenStandardOutput();
    var reader = new StreamReader(stdin, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);

    while (true)
    {
        // Read headers until empty line
        string? line;
        int contentLength = -1;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(':', 2);
                if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var len))
                    contentLength = len;
            }
        }
        if (contentLength < 0)
        {
            await Task.Delay(10);
            continue;
        }

        // Read body
        var buffer = ArrayPool<byte>.Shared.Rent(contentLength);
        try
        {
            int read = 0;
            while (read < contentLength)
            {
                int r = await stdin.ReadAsync(buffer, read, contentLength - read);
                if (r == 0) return; // EOF
                read += r;
            }
            var json = Encoding.UTF8.GetString(buffer, 0, contentLength);
            var resp = Handle(json, store);
            var respBytes = Encoding.UTF8.GetBytes(resp);
            var header = Encoding.ASCII.GetBytes($"Content-Length: {respBytes.Length}\r\n\r\n");
            await stdout.WriteAsync(header, 0, header.Length);
            await stdout.WriteAsync(respBytes, 0, respBytes.Length);
            await stdout.FlushAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling request");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}

static string Handle(string json, FileStore store)
{
    var req = JsonSerializer.Deserialize<RpcRequest>(json, JsonOpts.Options) ?? new RpcRequest { Id = "0", Method = "" };
    try
    {
        return req.Method switch
        {
            "mcp/initialize" => Serialize(Ok(req.Id, new { capabilities = new { } })),
            "ping" => Serialize(Ok(req.Id, new { ok = true, message = "pong" })),
            "fs/list" => Serialize(Ok(req.Id, new { files = FsList(req.Params, store) })),
            "fs/readText" => Serialize(Ok(req.Id, new { text = FsReadText(req.Params, store) })),
            "fs/writeText" => Serialize(Ok(req.Id, FsWriteText(req.Params, store))),
            _ => Serialize(Error(req.Id, -32601, $"Method not found: {req.Method}"))
        };
    }
    catch (Exception ex)
    {
        return Serialize(Error(req.Id, -32000, ex.Message));
    }
}

static object FsWriteText(JsonElement? @params, FileStore store)
{
    var p = @params ?? throw new InvalidOperationException("params required");
    var root = ParseRoot(p.GetProperty("root").GetString()!);
    var name = p.GetProperty("name").GetString()!;
    var content = p.GetProperty("content").GetString() ?? string.Empty;
    var exts = p.TryGetProperty("exts", out var exEl) && exEl.ValueKind == JsonValueKind.Array
        ? exEl.EnumerateArray().Select(e => e.GetString()!).Where(s => s is not null).ToArray()!
        : Array.Empty<string>();
    store.WriteText(root, name, content, exts);
    return new { ok = true };
}

static string FsReadText(JsonElement? @params, FileStore store)
{
    var p = @params ?? throw new InvalidOperationException("params required");
    var root = ParseRoot(p.GetProperty("root").GetString()!);
    var name = p.GetProperty("name").GetString()!;
    var exts = p.TryGetProperty("exts", out var exEl) && exEl.ValueKind == JsonValueKind.Array
        ? exEl.EnumerateArray().Select(e => e.GetString()!).Where(s => s is not null).ToArray()!
        : Array.Empty<string>();
    return store.ReadText(root, name, exts);
}

static IEnumerable<string> FsList(JsonElement? @params, FileStore store)
{
    var p = @params ?? throw new InvalidOperationException("params required");
    var root = ParseRoot(p.GetProperty("root").GetString()!);
    var exts = p.TryGetProperty("exts", out var exEl) && exEl.ValueKind == JsonValueKind.Array
        ? exEl.EnumerateArray().Select(e => e.GetString()!).Where(s => s is not null).ToArray()!
        : Array.Empty<string>();
    return store.List(root, exts).ToArray();
}

static StoreRoot ParseRoot(string value) => value?.ToLowerInvariant() switch
{
    "requirements" => StoreRoot.Requirements,
    "architecture" => StoreRoot.Architecture,
    "artifacts" => StoreRoot.Artifacts,
    _ => throw new InvalidOperationException($"Unknown root: {value}")
};

static RpcResponse Ok(string? id, object? result) => new() { Id = id, Result = result };
static RpcResponse Error(string? id, int code, string message) => new() { Id = id, Error = new RpcError { Code = code, Message = message } };
static string Serialize(RpcResponse resp) => JsonSerializer.Serialize(resp, JsonOpts.Options);

sealed class RpcRequest
{
    public string Jsonrpc { get; set; } = "2.0";
    public string Method { get; set; } = string.Empty;
    public JsonElement? Params { get; set; }
    public string? Id { get; set; }
}

sealed class RpcResponse
{
    public string Jsonrpc { get; set; } = "2.0";
    public object? Result { get; set; }
    public RpcError? Error { get; set; }
    public string? Id { get; set; }
}

sealed class RpcError
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
}

static class JsonOpts
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
