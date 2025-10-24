namespace CodeHero.McpServer;

public sealed class RpcError
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
}