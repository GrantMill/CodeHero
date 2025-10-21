var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.CodeHero_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

// Start MCP server (stdio) as a project; requires project in the solution so DCP can launch it
builder.AddProject<Projects.CodeHero_McpServer>("mcpserver");

// STT Whisper container (local) managed by Aspire (requires local image)
var stt = builder.AddContainer("stt-whisper", "codehero/stt-whisper:cpu")
    .WithHttpEndpoint(targetPort: 8000, name: "http")
    .WithEnvironment("WHISPER_MODEL", "small")
    .WithEnvironment("COMPUTE_TYPE", "int8")
    // Canonical Docker Desktop path so DCP can mkdir the parent
    .WithBindMount("/run/desktop/mnt/host/d/codehero/models/whisper", "/models", isReadOnly: false)
    .WithHttpHealthCheck("/health");

// Simple HTTP TTS container (placeholder tone) managed by Aspire
var tts = builder.AddContainer("tts-http", "codehero/tts-http:cpu")
    .WithHttpEndpoint(targetPort: 8000, name: "http")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.CodeHero_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WithEnvironment("Speech__Endpoint", stt.GetEndpoint("http"))
    .WithEnvironment("Tts__Endpoint", tts.GetEndpoint("http"))
    .WaitFor(stt)
    .WaitFor(tts)
    .WaitFor(apiService);

builder.Build().Run();
