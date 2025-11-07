using System.Text.Json;

namespace CodeHero.Web.Services;

public sealed class NullMcpClient : IMcpClient
{
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task<bool> InitializeAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task<bool> PingAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task<IReadOnlyList<string>> ListAsync(StoreRoot root, string[]? exts = null, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<string>)Array.Empty<string>());

    public Task<string> ReadTextAsync(StoreRoot root, string name, string[]? exts = null, CancellationToken ct = default) => Task.FromResult(string.Empty);

    public Task WriteTextAsync(StoreRoot root, string name, string content, string[]? exts = null, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<string>> ListAgentsAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<string>)new[] { "scribe" });

    public Task<JsonElement> GetAgentCapabilitiesAsync(string agent, CancellationToken ct = default)
    {
        // Return an empty object element that is safe to use after this method returns.
        using var doc = JsonDocument.Parse("{}");
        var cloned = doc.RootElement.Clone();
        return Task.FromResult(cloned);
    }

    public Task<string> ScribeCreateRequirementAsync(string id, string title, CancellationToken ct = default) => Task.FromResult("REQ-001.md");

    public Task<string> ScribeNextIdAsync(CancellationToken ct = default) => Task.FromResult("REQ-001");

    public Task<(string Id, string File, string Content)> ScribePreviewCreateRequirementAsync(string title, CancellationToken ct = default)
        => Task.FromResult(("REQ-001", "REQ-001.md", $"---\nid: REQ-001\ntitle: {title}\nstatus: draft\n---\nShort description.\n"));

    public Task<string> CodeDiffAsync(StoreRoot root, string name, string content, string? original = null, CancellationToken ct = default) => Task.FromResult(string.Empty);

    public Task CodeEditAsync(StoreRoot root, string name, string content, string? expectedDiff = null, CancellationToken ct = default) => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;

    // Minimal safe implementation of the low-level raw call helper required by IMcpClient.
    public Task<string> CallRawAsync(string method, object? @params = null, CancellationToken ct = default)
    {
        // Null client should not throw — return a minimal JSON response.
        // Callers expecting richer behavior should use a real McpClient.
        return Task.FromResult("{}");
    }
}