using CodeHero.Web;
using CodeHero.Web.Components;
using CodeHero.Web.Services;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http.Features;

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
    builder.Services.AddHttpClient("stt", c =>
    {
        c.BaseAddress = new Uri(whisperEndpoint);
        c.Timeout = TimeSpan.FromMinutes(2);
        c.DefaultRequestVersion = HttpVersion.Version11;
        c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        c.DefaultRequestHeaders.ExpectContinue = true;
    });

    builder.Services.AddHttpClient("tts", c =>
    {
        c.BaseAddress = new Uri(httpTtsEndpoint);
        c.Timeout = TimeSpan.FromMinutes(2);
        c.DefaultRequestVersion = HttpVersion.Version11;
        c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        c.DefaultRequestHeaders.ExpectContinue = true;
    });
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

// Request limits/timeouts
const long SttMaxBytes = 10L * 1024 * 1024; // 10 MB
const long TtsMaxBytes = 256L * 1024; // 256 KB
var sttTimeout = TimeSpan.FromSeconds(60);
var ttsTimeout = TimeSpan.FromSeconds(30);

// Minimal TTS/STT endpoints (optional)
var enableSpeechApi = builder.Configuration.GetValue("Features:EnableSpeechApi", app.Environment.IsDevelopment());
if (enableSpeechApi)
{
    app.MapPost("/api/tts", async (ISpeechService speech, HttpContext ctx) =>
    {
        // Disable caching
        ctx.Response.Headers.CacheControl = "no-store, max-age=0, must-revalidate";
        ctx.Response.Headers.Pragma = "no-cache";
        ctx.Response.Headers.Expires = "0";

        // Enforce request size limit (by feature + header check)
        ctx.Features.Get<IHttpMaxRequestBodySizeFeature>()?.Let(f => f.MaxRequestBodySize = TtsMaxBytes);
        if (ctx.Request.ContentLength.HasValue && ctx.Request.ContentLength.Value > TtsMaxBytes)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
        cts.CancelAfter(ttsTimeout);

        using var reader = new StreamReader(ctx.Request.Body);
#if NET8_0_OR_GREATER
            var text = await reader.ReadToEndAsync(cts.Token);
#else
            var text = await reader.ReadToEndAsync();
#endif
        var voice = ctx.Request.Query["voice"].FirstOrDefault() ?? "en-US-JennyNeural";
        var audio = await speech.SynthesizeAsync(text, voice, ct: cts.Token);
        return Results.File(audio, "audio/wav");
    }).DisableAntiforgery();

    app.MapPost("/api/stt", async (ISpeechService speech, HttpContext ctx) =>
    {
        // Disable caching
        ctx.Response.Headers.CacheControl = "no-store, max-age=0, must-revalidate";
        ctx.Response.Headers.Pragma = "no-cache";
        ctx.Response.Headers.Expires = "0";

        // Enforce request size limit (by feature + stream enforcement)
        ctx.Features.Get<IHttpMaxRequestBodySizeFeature>()?.Let(f => f.MaxRequestBodySize = SttMaxBytes);
        if (ctx.Request.ContentLength.HasValue && ctx.Request.ContentLength.Value > SttMaxBytes)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
        cts.CancelAfter(sttTimeout);

        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            int read = await ctx.Request.Body.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token);
            if (read == 0) break;
            total += read;
            if (total > SttMaxBytes)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            await ms.WriteAsync(buffer.AsMemory(0, read), cts.Token);
        }

        var text = await speech.TranscribeAsync(ms.ToArray(), ct: cts.Token);
        return Results.Text(text);
    }).DisableAntiforgery();
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
    }).DisableAntiforgery();
}

app.MapDefaultEndpoints();

app.Run();

// small helper to avoid null checks for feature-setting
file static class FeatureExt
{
    public static void Let<T>(this T? obj, Action<T> action) where T : class
    {
        if (obj is not null) action(obj);
    }
}
