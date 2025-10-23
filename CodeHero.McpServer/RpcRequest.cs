namespace CodeHero.McpServer;

public sealed class RpcRequest
{
    public string Jsonrpc { get; set; } = "2.0";
    public string Method { get; set; } = string.Empty;
    public System.Text.Json.JsonElement? Params { get; set; }
    public string? Id { get; set; }
}
