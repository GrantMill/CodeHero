namespace CodeHero.Web.Services;

public interface IMcpClient : IAsyncDisposable
{
    Task<bool> InitializeAsync(CancellationToken ct = default);

    Task<bool> PingAsync(CancellationToken ct = default);

    Task<IReadOnlyList<string>> ListAsync(StoreRoot root, string[]? exts = null, CancellationToken ct = default);

    Task<string> ReadTextAsync(StoreRoot root, string name, string[]? exts = null, CancellationToken ct = default);

    Task WriteTextAsync(StoreRoot root, string name, string content, string[]? exts = null, CancellationToken ct = default);

    Task<IReadOnlyList<string>> ListAgentsAsync(CancellationToken ct = default);

    Task<System.Text.Json.JsonElement> GetAgentCapabilitiesAsync(string agent, CancellationToken ct = default);

    Task<string> ScribeCreateRequirementAsync(string id, string title, CancellationToken ct = default);

    // Approval helpers
    Task<string> ScribeNextIdAsync(CancellationToken ct = default);

    Task<(string Id, string File, string Content)> ScribePreviewCreateRequirementAsync(string title, CancellationToken ct = default);

    Task<string> CodeDiffAsync(StoreRoot root, string name, string content, string? original = null, CancellationToken ct = default);

    Task CodeEditAsync(StoreRoot root, string name, string content, string? expectedDiff = null, CancellationToken ct = default);

    Task ShutdownAsync(CancellationToken ct = default);

    // Low-level raw call helper
    Task<string> CallRawAsync(string method, object? @params = null, CancellationToken ct = default);
}