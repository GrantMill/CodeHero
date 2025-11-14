using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace CodeHero.McpServer;

internal sealed class SimpleWebEnv : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = default!;
    public string ContentRootPath { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public IFileProvider WebRootFileProvider { get; set; } = default!;
    public string WebRootPath { get; set; } = string.Empty;
}