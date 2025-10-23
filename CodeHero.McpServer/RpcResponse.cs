namespace CodeHero.McpServer;

public sealed class RpcResponse
{
    public string Jsonrpc { get; set; } = "2.0";
    public object? Result { get; set; }
    public RpcError? Error { get; set; }
    public string? Id { get; set; }
}
