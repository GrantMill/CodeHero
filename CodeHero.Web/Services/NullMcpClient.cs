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
 public Task<JsonElement> GetAgentCapabilitiesAsync(string agent, CancellationToken ct = default) => Task.FromResult(JsonDocument.Parse("{}").RootElement);
 public Task<string> ScribeCreateRequirementAsync(string id, string title, CancellationToken ct = default) => Task.FromResult("REQ-001.md");
 public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
}
