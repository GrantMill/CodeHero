using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeHero.Tests;

[TestClass]
public class McpServerTests
{
    static (Process proc, Stream stdin, Stream stdout) Start()
    {
        var baseDir = AppContext.BaseDirectory; // .../CodeHero.Tests/bin/Debug/net10.0/
        var serverDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "CodeHero.McpServer", "bin", "Debug", "net10.0"));
        var dll = Path.Combine(serverDir, "CodeHero.McpServer.dll");
        if (!File.Exists(dll)) Assert.Inconclusive("MCP server not built: " + dll);

        var psi = new ProcessStartInfo("dotnet", $"\"{dll}\"")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = serverDir,
        };
        var p = Process.Start(psi)!;
        return (p, p.StandardInput.BaseStream, p.StandardOutput.BaseStream);
    }

    static async Task SendAsync(Stream stdin, object req)
    {
        var json = JsonSerializer.Serialize(req);
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        await stdin.WriteAsync(header, 0, header.Length);
        await stdin.WriteAsync(bytes, 0, bytes.Length);
        await stdin.FlushAsync();
    }

    static async Task<string> ReadAsync(Stream stdout)
    {
        // Read until CRLFCRLF for headers
        var headerBuf = new MemoryStream();
        var tmp = new byte[1];
        var last4 = new byte[4];
        int count = 0;
        while (true)
        {
            var r = await stdout.ReadAsync(tmp, 0, 1);
            Assert.IsTrue(r > 0, "EOF while reading headers");
            headerBuf.Write(tmp, 0, 1);
            last4[count % 4] = tmp[0];
            count++;
            if (count >= 4 && last4[(count - 4) % 4] == (byte)'\r' && last4[(count - 3) % 4] == (byte)'\n' && last4[(count - 2) % 4] == (byte)'\r' && last4[(count - 1) % 4] == (byte)'\n')
                break;
        }
        var headerText = Encoding.ASCII.GetString(headerBuf.ToArray());
        var lenLine = headerText.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                                .FirstOrDefault(l => l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(lenLine, "Missing Content-Length");
        var lenStr = lenLine.Split(':', 2)[1].Trim();
        Assert.IsTrue(int.TryParse(lenStr, out var len), "Invalid Content-Length");

        var body = new byte[len];
        var read = 0;
        while (read < len)
        {
            var r = await stdout.ReadAsync(body, read, len - read);
            Assert.IsTrue(r > 0, "EOF while reading body");
            read += r;
        }
        return Encoding.UTF8.GetString(body, 0, len);
    }

    [TestMethod]
    public async Task Initialize_And_Ping_Works()
    {
        var (proc, sin, sout) = Start();
        try
        {
            await SendAsync(sin, new { jsonrpc = "2.0", method = "mcp/initialize", id = "1" });
            _ = await ReadAsync(sout);

            await SendAsync(sin, new { jsonrpc = "2.0", method = "ping", id = "2" });
            var pong = await ReadAsync(sout);
            StringAssert.Contains(pong, "\"pong\"");
        }
        finally
        {
            try { proc.Kill(true); } catch { }
        }
    }
}
