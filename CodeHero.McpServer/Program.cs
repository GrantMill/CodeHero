using System.Buffers;
using System.Text;
using System.Text.Json;
using CodeHero.Web.Services;
using Microsoft.AspNetCore.Hosting;
using CodeHero.McpServer;
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
        // Provide a minimal IWebHostEnvironment for FileStore in a console host
        services.AddSingleton<IWebHostEnvironment>(sp => new SimpleWebEnv
        {
            ApplicationName = "CodeHero.McpServer",
            EnvironmentName = ctx.HostingEnvironment.EnvironmentName,
            ContentRootPath = Directory.GetCurrentDirectory(),
            ContentRootFileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Directory.GetCurrentDirectory()),
            WebRootFileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Directory.GetCurrentDirectory()),
            WebRootPath = null
        });

        // FileStore will be constructed on demand in fs/* handlers to avoid startup config requirements.
    })
    .Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MCP");
var cfg = host.Services.GetRequiredService<IConfiguration>();
// Defer FileStore resolution until fs/* methods are called.

logger.LogInformation("MCP server starting");

await RunAsync(host.Services, logger);

static async Task RunAsync(IServiceProvider services, ILogger logger)
{
    var stdin = Console.OpenStandardInput();
    var stdout = Console.OpenStandardOutput();

    while (true)
    {
        // Read headers (binary) until CRLFCRLF
        int contentLength = -1;
        using (var headerBuf = new MemoryStream())
        {
            var last4 = new byte[4];
            var one = new byte[1];
            int count = 0;
            while (true)
            {
                int r = await stdin.ReadAsync(one, 0, 1);
                if (r == 0) return; // EOF
                headerBuf.Write(one, 0, 1);
                last4[count % 4] = one[0];
                count++;
                if (count >= 4 && last4[(count - 4) % 4] == (byte)'\r' && last4[(count - 3) % 4] == (byte)'\n' && last4[(count - 2) % 4] == (byte)'\r' && last4[(count - 1) % 4] == (byte)'\n')
                    break;
            }
            var headerText = Encoding.ASCII.GetString(headerBuf.ToArray());
            var lenLine = headerText.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                                    .FirstOrDefault(h => h.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
            if (lenLine is null || !int.TryParse(lenLine.Split(':', 2)[1].Trim(), out contentLength))
            {
                // Invalid request; ignore and continue
                continue;
            }
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
            var resp = Handle(json, services);
            var respBytes = Encoding.UTF8.GetBytes(resp);
            var header = Encoding.ASCII.GetBytes($"Content-Length: {respBytes.Length}\r\n\r\n");
            await stdout.WriteAsync(header, 0, header.Length);
            await stdout.WriteAsync(respBytes, 0, respBytes.Length);
            await stdout.FlushAsync();
            if (ServerState.Shutdown) return;
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

static string ShutdownResponse(string? id)
{
    ServerState.Shutdown = true;
    return Serialize(Ok(id, new { ok = true }));
}

static object DescribeAgent(JsonElement? @params)
{
    var name = @params.HasValue && @params.Value.TryGetProperty("agent", out var a) ? (a.GetString() ?? string.Empty) : "scribe";
    if (!string.Equals(name, "scribe", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Unknown agent: {name}");
    return new
    {
        agent = "scribe",
        tools = new object[]
        {
            new { name = "scribe/createRequirement", description = "Create a new requirement file with frontmatter and a stub.", parameters = new { id = "REQ-###", title = "string" } }
        }
    };
}

static object ScribeCreateRequirement(JsonElement? @params, IServiceProvider services)
{
    var p = @params ?? throw new InvalidOperationException("params required");
    var id = p.TryGetProperty("id", out var idEl) ? (idEl.GetString() ?? "REQ-001") : "REQ-001";
    var title = p.TryGetProperty("title", out var tEl) ? (tEl.GetString() ?? "New Requirement") : "New Requirement";
    var file = id.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? id : id + ".md";
    var content = $"---\nid: {id}\ntitle: {title}\nstatus: draft\n---\nShort description.\n";
    var store = new FileStore(services.GetRequiredService<IConfiguration>(), services.GetRequiredService<IWebHostEnvironment>());
    store.WriteText(StoreRoot.Requirements, file, content, ".md");
    return new { created = file };
}

static string Handle(string json, IServiceProvider services)
{
    var req = JsonSerializer.Deserialize<RpcRequest>(json, JsonOpts.Options) ?? new RpcRequest { Id = "0", Method = "" };
    try
    {
        return req.Method switch
        {
            "mcp/initialize" => Serialize(Ok(req.Id, new { capabilities = new { } })),
            "mcp/shutdown" => ShutdownResponse(req.Id),
            "ping" => Serialize(Ok(req.Id, new { ok = true, message = "pong" })),
            // Agents discovery and capabilities
            "agents/list" => Serialize(Ok(req.Id, new { agents = new[] { "scribe" } })),
            "agents/capabilities" => Serialize(Ok(req.Id, DescribeAgent(req.Params))),
            // Scribe tools
            "scribe/createRequirement" => Serialize(Ok(req.Id, ScribeCreateRequirement(req.Params, services))),
            "fs/list" => Serialize(Ok(req.Id, new { files = FsList(req.Params, services) })),
            "fs/readText" => Serialize(Ok(req.Id, new { text = FsReadText(req.Params, services) })),
            "fs/writeText" => Serialize(Ok(req.Id, FsWriteText(req.Params, services))),
            _ => Serialize(Error(req.Id, -32601, $"Method not found: {req.Method}"))
        };
    }
    catch (Exception ex)
    {
        return Serialize(Error(req.Id, -32000, ex.Message));
    }
}

static object FsWriteText(JsonElement? @params, IServiceProvider services)
{
    var p = @params ?? throw new InvalidOperationException("params required");
    var root = ParseRoot(p.GetProperty("root").GetString()!);
    var name = p.GetProperty("name").GetString()!;
    var content = p.GetProperty("content").GetString() ?? string.Empty;
    var exts = p.TryGetProperty("exts", out var exEl) && exEl.ValueKind == JsonValueKind.Array
        ? exEl.EnumerateArray().Select(e => e.GetString()!).Where(s => s is not null).ToArray()!
        : Array.Empty<string>();
    var store = new FileStore(services.GetRequiredService<IConfiguration>(), services.GetRequiredService<IWebHostEnvironment>());
    store.WriteText(root, name, content, exts);
    return new { ok = true };
}

static string FsReadText(JsonElement? @params, IServiceProvider services)
{
    var p = @params ?? throw new InvalidOperationException("params required");
    var root = ParseRoot(p.GetProperty("root").GetString()!);
    var name = p.GetProperty("name").GetString()!;
    var exts = p.TryGetProperty("exts", out var exEl) && exEl.ValueKind == JsonValueKind.Array
        ? exEl.EnumerateArray().Select(e => e.GetString()!).Where(s => s is not null).ToArray()!
        : Array.Empty<string>();
    var store = new FileStore(services.GetRequiredService<IConfiguration>(), services.GetRequiredService<IWebHostEnvironment>());
    return store.ReadText(root, name, exts);
}

static IEnumerable<string> FsList(JsonElement? @params, IServiceProvider services)
{
    var p = @params ?? throw new InvalidOperationException("params required");
    var root = ParseRoot(p.GetProperty("root").GetString()!);
    var exts = p.TryGetProperty("exts", out var exEl) && exEl.ValueKind == JsonValueKind.Array
        ? exEl.EnumerateArray().Select(e => e.GetString()!).Where(s => s is not null).ToArray()!
        : Array.Empty<string>();
    var store = new FileStore(services.GetRequiredService<IConfiguration>(), services.GetRequiredService<IWebHostEnvironment>());
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
