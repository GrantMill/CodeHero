var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.CodeHero_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

// Start MCP server (stdio). No network endpoints; background utility process.
builder.AddProject<Projects.CodeHero_McpServer>("mcpserver");

builder.AddProject<Projects.CodeHero_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
