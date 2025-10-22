using System.Text.Json;
using CodeHero.Web.Services;

namespace CodeHero.Tests;

[TestClass]
public class McpOrchestratorAgentServiceTests
{
 private sealed class FakeMcp : IMcpClient
 {
 public List<string> Files { get; } = new() { "REQ-000.md", "REQ-001.md" };
 public Dictionary<string,string> FileMap { get; } = new()
 {
 ["REQ-000.md"] = "--- REQ-000 ---\nBootstrap baseline",
 ["REQ-001.md"] = "--- REQ-001 ---\nSomething else",
 };

 public ValueTask DisposeAsync() => ValueTask.CompletedTask;
 public Task<bool> InitializeAsync(CancellationToken ct = default) => Task.FromResult(true);
 public Task<bool> PingAsync(CancellationToken ct = default) => Task.FromResult(true);
 public Task<IReadOnlyList<string>> ListAsync(StoreRoot root, string[]? exts = null, CancellationToken ct = default)
 => Task.FromResult((IReadOnlyList<string>)Files.ToArray());
 public Task<string> ReadTextAsync(StoreRoot root, string name, string[]? exts = null, CancellationToken ct = default)
 => Task.FromResult(FileMap.TryGetValue(name, out var s) ? s : string.Empty);
 public Task WriteTextAsync(StoreRoot root, string name, string content, string[]? exts = null, CancellationToken ct = default)
 => Task.CompletedTask;
 public Task<IReadOnlyList<string>> ListAgentsAsync(CancellationToken ct = default)
 => Task.FromResult((IReadOnlyList<string>)Array.Empty<string>());
 public Task<JsonElement> GetAgentCapabilitiesAsync(string agent, CancellationToken ct = default)
 => Task.FromResult(JsonDocument.Parse("{}").RootElement);
 public Task<string> ScribeCreateRequirementAsync(string id, string title, CancellationToken ct = default)
 => Task.FromResult($"REQ-{id}.md");
 public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
 }

 [TestMethod]
 public async Task ListRequirements_Variants()
 {
 var svc = new McpOrchestratorAgentService(new FakeMcp());
 foreach (var input in new[] { "list req", "list requirements", "LIST REQUIREMENTS.", "show wreck" })
 {
 var reply = await svc.ChatAsync(input);
 StringAssert.Contains(reply, "REQ-000.md");
 }
 }

 [TestMethod]
 public async Task ReadRequirement_Spoken_ZeroZeroZero()
 {
 var svc = new McpOrchestratorAgentService(new FakeMcp());
 var reply = await svc.ChatAsync("read requirement zero zero zero");
 StringAssert.Contains(reply, "REQ-000.md");
 }

 [TestMethod]
 public async Task ReadRequirement_Mishear_Wreck()
 {
 var svc = new McpOrchestratorAgentService(new FakeMcp());
 var reply = await svc.ChatAsync("read wreck dashed zero zero one");
 StringAssert.Contains(reply, "REQ-001.md");
 }

 [TestMethod]
 public async Task ReadRequirement_Direct_Filename()
 {
 var svc = new McpOrchestratorAgentService(new FakeMcp());
 var reply = await svc.ChatAsync("read REQ-001.md");
 StringAssert.Contains(reply, "REQ-001.md");
 }
}
