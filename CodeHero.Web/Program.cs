using CodeHero.Web;
using CodeHero.Web.Components;
using CodeHero.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

// Repo file I/O service (whitelisted roots configured in appsettings)
builder.Services.AddSingleton<FileStore>();
builder.Services.AddSingleton<IMcpClient, McpClient>();
builder.Services.AddHttpClient();

// Conditionally wire up speech service based on configuration presence
var speechKey = builder.Configuration["AzureAI:Speech:Key"];
var speechRegion = builder.Configuration["AzureAI:Speech:Region"];
var whisperEndpoint = builder.Configuration["Speech:Endpoint"]; // local Whisper container
var httpTtsEndpoint = builder.Configuration["Tts:Endpoint"]; // local HTTP TTS container
var foundryKey = builder.Configuration["AzureAI:Foundry:Key"];
var foundryEndpoint = builder.Configuration["AzureAI:Foundry:Endpoint"];
var foundryTranscribe = builder.Configuration["AzureAI:Foundry:TranscribeDeployment"];

if (!string.IsNullOrWhiteSpace(whisperEndpoint) && !string.IsNullOrWhiteSpace(httpTtsEndpoint))
{
    // Whisper (STT) + HTTP TTS
    builder.Services.AddHttpClient();
    builder.Services.AddSingleton<NullSpeechService>();
    builder.Services.AddSingleton<ISpeechService, WhisperAndHttpTtsSpeechService>();
}
else if (!string.IsNullOrWhiteSpace(whisperEndpoint))
{
    builder.Services.AddHttpClient<ISpeechService, WhisperClientSpeechService>(c =>
    {
        c.BaseAddress = new Uri(whisperEndpoint);
        c.Timeout = TimeSpan.FromMinutes(2);
    });
    builder.Services.AddSingleton<NullSpeechService>();
}
else if (!string.IsNullOrWhiteSpace(speechKey) && !string.IsNullOrWhiteSpace(speechRegion) &&
    !string.IsNullOrWhiteSpace(foundryKey) && !string.IsNullOrWhiteSpace(foundryEndpoint))
{
    // Use Azure Speech for TTS and Foundry gpt-4o-transcribe-diarize for STT
    builder.Services.AddHttpClient();
    builder.Services.AddSingleton<AzureSpeechService>();
    builder.Services.AddSingleton<FoundryTranscribeService>();
    builder.Services.AddSingleton<ISpeechService, CombinedSpeechService>();
}
else if (!string.IsNullOrWhiteSpace(foundryKey) && !string.IsNullOrWhiteSpace(foundryEndpoint))
{
    // Foundry-only: use Foundry for STT and silent WAV for TTS
    builder.Services.AddHttpClient();
    builder.Services.AddSingleton<FoundryTranscribeService>();
    builder.Services.AddSingleton<NullSpeechService>();
    builder.Services.AddSingleton<ISpeechService, FoundrySpeechService>();
}
else if (!string.IsNullOrWhiteSpace(speechKey) && !string.IsNullOrWhiteSpace(speechRegion))
{
    builder.Services.AddSingleton<ISpeechService, AzureSpeechService>();
}
else
{
    builder.Services.AddSingleton<ISpeechService, NullSpeechService>();
}

// Conditionally wire up agent service based on configuration presence
if (!string.IsNullOrWhiteSpace(foundryKey) && !string.IsNullOrWhiteSpace(foundryEndpoint))
{
    builder.Services.AddSingleton<IAgentService, AzureFoundryAgentService>();
}
else
{
    builder.Services.AddSingleton<IAgentService, NullAgentService>();
}

// Remove default sample HttpClient (Weather)

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Allow disabling HTTPS redirection via config/env (e.g., in CI test runs)
var disableHttpsRedirect = builder.Configuration.GetValue<bool>("Server:DisableHttpsRedirection");
if (!disableHttpsRedirect)
{
    app.UseHttpsRedirection();
}

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Minimal TTS/STT endpoints (optional)
var enableSpeechApi = builder.Configuration.GetValue("Features:EnableSpeechApi", app.Environment.IsDevelopment());
if (enableSpeechApi)
{
    app.MapPost("/api/tts", async (ISpeechService speech, HttpContext ctx) =>
    {
        var text = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
        var voice = ctx.Request.Query["voice"].FirstOrDefault() ?? "en-US-JennyNeural";
        var audio = await speech.SynthesizeAsync(text, voice, ct: ctx.RequestAborted);
        return Results.File(audio, "audio/wav");
    });

    app.MapPost("/api/stt", async (ISpeechService speech, HttpContext ctx) =>
    {
        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms, ctx.RequestAborted);
        var text = await speech.TranscribeAsync(ms.ToArray(), ct: ctx.RequestAborted);
        return Results.Text(text);
    });
}

// Minimal Agent chat endpoint (dev/demo)
var enableAgentApi = builder.Configuration.GetValue("Features:EnableAgentApi", app.Environment.IsDevelopment());
if (enableAgentApi)
{
    app.MapPost("/api/agent/chat", async (IAgentService agent, HttpContext ctx) =>
    {
        var text = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
        var reply = await agent.ChatAsync(text, ctx.RequestAborted);
        return Results.Text(reply);
    });
}

app.MapDefaultEndpoints();

app.Run();
