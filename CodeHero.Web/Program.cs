using CodeHero.Web;
using CodeHero.Web.Components;
using CodeHero.Web.Services;
using Markdig;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Polly;
using Polly.Extensions.Http;
using System.Diagnostics;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
 .AddInteractiveServerComponents();

// Register Markdig MarkdownPipeline for injection into components
builder.Services.AddSingleton<Markdig.MarkdownPipeline>(sp =>
{
    return new Markdig.MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .Build();
});

// Response compression for SignalR (_blazor) payloads
builder.Services.AddResponseCompression(options =>
{
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/octet-stream" });
});

// Tune SignalR/Blazor Server hub to reduce WS disconnects (applies to all hubs)
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 2 * 1024 * 1024; //2 MB
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
});

builder.Services.AddOutputCache();

// Repo file I/O service (whitelisted roots configured in appsettings)
builder.Services.AddSingleton<FileStore>();
builder.Services.AddSingleton<IMcpClient, McpClient>();
builder.Services.AddHttpClient();
// Diagnostics monitor to show last call status
builder.Services.AddSingleton<SpeechDiagnosticsMonitor>();

// Resilience policies for outbound STT/TTS HTTP
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() => HttpPolicyExtensions
 .HandleTransientHttpError() //5xx + HttpRequestException +408
 .OrResult(r => (int)r.StatusCode == 429)
 .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt))); // exp backoff

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() => HttpPolicyExtensions
 .HandleTransientHttpError()
 .CircuitBreakerAsync(5, TimeSpan.FromSeconds(60));

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
    })
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

    builder.Services.AddHttpClient("tts", c =>
    {
        c.BaseAddress = new Uri(httpTtsEndpoint);
        c.Timeout = TimeSpan.FromMinutes(2);
        c.DefaultRequestVersion = HttpVersion.Version11;
        c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        c.DefaultRequestHeaders.ExpectContinue = true;
    })
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());
    builder.Services.AddSingleton<NullSpeechService>();
    builder.Services.AddSingleton<ISpeechService, WhisperAndHttpTtsSpeechService>();
}
else if (!string.IsNullOrWhiteSpace(whisperEndpoint))
{
    builder.Services.AddHttpClient<ISpeechService, WhisperClientSpeechService>(c =>
    {
        c.BaseAddress = new Uri(whisperEndpoint);
        c.Timeout = TimeSpan.FromMinutes(2);
    })
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());
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

// Agent service: use LLM orchestrator (supports Foundry models and deployments routes)
builder.Services.AddSingleton<IAgentService, LlmOrchestratorAgentService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
const long SttMaxBytes = 10L * 1024 * 1024; //10 MB
const long TtsMaxBytes = 256L * 1024; //256 KB
var sttTimeout = TimeSpan.FromSeconds(60);
var ttsTimeout = TimeSpan.FromSeconds(30);

// Minimal TTS/STT endpoints (optional)
var enableSpeechApi = builder.Configuration.GetValue("Features:EnableSpeechApi", app.Environment.IsDevelopment());
if (enableSpeechApi)
{
    app.MapPost("/api/tts", async (ISpeechService speech, HttpContext ctx, ILoggerFactory lf, SpeechDiagnosticsMonitor diag) =>
    {
        var log = lf.CreateLogger("Api.TTS");
        var sw = Stopwatch.StartNew();
        int status = StatusCodes.Status200OK;
        long reqBytes = ctx.Request.ContentLength ?? -1;
        long respBytes = 0;
        string voice = ctx.Request.Query["voice"].FirstOrDefault() ?? "en-US-JennyNeural";

        // Disable caching
        ctx.Response.Headers.CacheControl = "no-store, max-age=0, must-revalidate";
        ctx.Response.Headers.Pragma = "no-cache";
        ctx.Response.Headers.Expires = "0";

        // Enforce request size limit (by feature + header check)
        ctx.Features.Get<IHttpMaxRequestBodySizeFeature>()?.Let(f => f.MaxRequestBodySize = TtsMaxBytes);
        if (ctx.Request.ContentLength.HasValue && ctx.Request.ContentLength.Value > TtsMaxBytes)
        {
            status = StatusCodes.Status413PayloadTooLarge;
            sw.Stop();
            log.LogWarning("TTS rejected: payload too large. Status={Status} DurationMs={DurationMs} ReqBytes={ReqBytes}", status, sw.ElapsedMilliseconds, reqBytes);
            SpeechTelemetry.TtsErrors.Add(1);
            SpeechTelemetry.TtsRequestBytes.Record(reqBytes);
            SpeechTelemetry.TtsDurationMs.Record(sw.Elapsed.TotalMilliseconds);
            diag.UpdateTts(status, reqBytes, 0, sw.Elapsed.TotalMilliseconds, "Payload too large");
            return Results.StatusCode(status);
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
            cts.CancelAfter(ttsTimeout);
            using var activity = SpeechTelemetry.Activity.StartActivity("api.tts");
            activity?.SetTag("voice", voice);
            activity?.SetTag("req.bytes", reqBytes);

            using var reader = new StreamReader(ctx.Request.Body);
#if NET8_0_OR_GREATER
            var text = await reader.ReadToEndAsync(cts.Token);
#else
 var text = await reader.ReadToEndAsync();
#endif
            var audio = await speech.SynthesizeAsync(text, voice, ct: cts.Token);
            respBytes = audio?.LongLength ?? 0;
            sw.Stop();
            log.LogInformation("TTS completed. Status={Status} DurationMs={DurationMs} ReqBytes={ReqBytes} RespBytes={RespBytes} Voice={Voice}", status, sw.ElapsedMilliseconds, reqBytes, respBytes, voice);
            SpeechTelemetry.TtsRequestBytes.Record(reqBytes < 0 ? 0 : reqBytes);
            SpeechTelemetry.TtsResponseBytes.Record(respBytes);
            SpeechTelemetry.TtsDurationMs.Record(sw.Elapsed.TotalMilliseconds);
            diag.UpdateTts(status, reqBytes, respBytes, sw.Elapsed.TotalMilliseconds);
            return Results.File(audio, "audio/wav");
        }
        catch (OperationCanceledException)
        {
            status = 499; // client closed request / timeout
            sw.Stop();
            log.LogWarning("TTS canceled. Status={Status} DurationMs={DurationMs} ReqBytes={ReqBytes}", status, sw.ElapsedMilliseconds, reqBytes);
            SpeechTelemetry.TtsErrors.Add(1);
            SpeechTelemetry.TtsDurationMs.Record(sw.Elapsed.TotalMilliseconds);
            diag.UpdateTts(status, reqBytes, 0, sw.Elapsed.TotalMilliseconds, "Canceled");
            return Results.StatusCode(status);
        }
        catch (Exception ex)
        {
            status = StatusCodes.Status500InternalServerError;
            sw.Stop();
            log.LogError(ex, "TTS failed. Status={Status} DurationMs={DurationMs} ReqBytes={ReqBytes}", status, sw.ElapsedMilliseconds, reqBytes);
            SpeechTelemetry.TtsErrors.Add(1);
            SpeechTelemetry.TtsDurationMs.Record(sw.Elapsed.TotalMilliseconds);
            diag.UpdateTts(status, reqBytes, 0, sw.Elapsed.TotalMilliseconds, ex.Message);
            return Results.Problem("TTS failed");
        }
    }).DisableAntiforgery();

    app.MapPost("/api/stt", async (ISpeechService speech, HttpContext ctx, ILoggerFactory lf, SpeechDiagnosticsMonitor diag) =>
    {
        var log = lf.CreateLogger("Api.STT");
        var sw = Stopwatch.StartNew();
        int status = StatusCodes.Status200OK;
        long reqBytes = 0;
        int respChars = 0;

        // Disable caching
        ctx.Response.Headers.CacheControl = "no-store, max-age=0, must-revalidate";
        ctx.Response.Headers.Pragma = "no-cache";
        ctx.Response.Headers.Expires = "0";

        // Enforce request size limit (by feature + stream enforcement)
        ctx.Features.Get<IHttpMaxRequestBodySizeFeature>()?.Let(f => f.MaxRequestBodySize = SttMaxBytes);
        if (ctx.Request.ContentLength.HasValue && ctx.Request.ContentLength.Value > SttMaxBytes)
        {
            status = StatusCodes.Status413PayloadTooLarge;
            sw.Stop();
            log.LogWarning("STT rejected: payload too large. Status=={Status} DurationMs={DurationMs} ReqBytes={ReqBytes}", status, sw.ElapsedMilliseconds, ctx.Request.ContentLength);
            SpeechTelemetry.SttErrors.Add(1);
            SpeechTelemetry.SttRequestBytes.Record(ctx.Request.ContentLength ?? 0);
            SpeechTelemetry.SttDurationMs.Record(sw.Elapsed.TotalMilliseconds);
            diag.UpdateStt(status, ctx.Request.ContentLength ?? 0, 0, sw.Elapsed.TotalMilliseconds, "Payload too large");
            return Results.StatusCode(status);
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
            cts.CancelAfter(sttTimeout);
            using var activity = SpeechTelemetry.Activity.StartActivity("api.stt");

            using var ms = new MemoryStream();
            var buffer = new byte[81920];
            while (true)
            {
                int read = await ctx.Request.Body.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token);
                if (read == 0) break;
                reqBytes += read;
                if (reqBytes > SttMaxBytes)
                {
                    status = StatusCodes.Status413PayloadTooLarge;
                    sw.Stop();
                    log.LogWarning("STT rejected: payload grew too large. Status={Status} DurationMs={DurationMs} ReqBytes={ReqBytes}", status, sw.ElapsedMilliseconds, reqBytes);
                    SpeechTelemetry.SttErrors.Add(1);
                    SpeechTelemetry.SttRequestBytes.Record(reqBytes);
                    SpeechTelemetry.SttDurationMs.Record(sw.Elapsed.TotalMilliseconds);
                    diag.UpdateStt(status, reqBytes, 0, sw.Elapsed.TotalMilliseconds, "Payload too large");
                    return Results.StatusCode(status);
                }
                await ms.WriteAsync(buffer.AsMemory(0, read), cts.Token);
            }

            var text = await speech.TranscribeAsync(ms.ToArray(), ct: cts.Token);
            respChars = text?.Length ?? 0;
            sw.Stop();
            log.LogInformation("STT completed. Status={Status} DurationMs={DurationMs} ReqBytes={ReqBytes} RespChars={RespChars}", status, sw.ElapsedMilliseconds, reqBytes, respChars);
            SpeechTelemetry.SttRequestBytes.Record(reqBytes);
            SpeechTelemetry.SttResponseChars.Record(respChars);
            SpeechTelemetry.SttDurationMs.Record(sw.Elapsed.TotalMilliseconds);
            diag.UpdateStt(status, reqBytes, respChars, sw.Elapsed.TotalMilliseconds);
            return Results.Text(text);
        }
        catch (OperationCanceledException)
        {
            status = 499; // client closed request / timeout
            sw.Stop();
            log.LogWarning("STT canceled. Status={Status} DurationMs={DurationMs} ReqBytes={ReqBytes}", status, sw.ElapsedMilliseconds, reqBytes);
            SpeechTelemetry.SttErrors.Add(1);
            SpeechTelemetry.SttDurationMs.Record(sw.Elapsed.TotalMilliseconds);
            diag.UpdateStt(status, reqBytes, 0, sw.Elapsed.TotalMilliseconds, "Canceled");
            return Results.StatusCode(status);
        }
        catch (Exception ex)
        {
            status = StatusCodes.Status500InternalServerError;
            sw.Stop();
            log.LogError(ex, "STT failed. Status={Status} DurationMs={DurationMs} ReqBytes={ReqBytes}", status, sw.ElapsedMilliseconds, reqBytes);
            SpeechTelemetry.SttErrors.Add(1);
            SpeechTelemetry.SttDurationMs.Record(sw.Elapsed.TotalMilliseconds);
            diag.UpdateStt(status, reqBytes, 0, sw.Elapsed.TotalMilliseconds, ex.Message);
            return Results.Problem("STT failed");
        }
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

// Simple diagnostics page (optional)
app.MapGet("/diagnostics/speech", (IConfiguration cfg, SpeechDiagnosticsMonitor diag) =>
{
    var tts = diag.GetLastTts();
    var stt = diag.GetLastStt();
    var report = $"" +
    $"Speech:Endpoint = {cfg["Speech:Endpoint"]}\n" +
    $"Tts:Endpoint = {cfg["Tts:Endpoint"]}\n" +
    $"AzureAI:Foundry:Endpoint = {cfg["AzureAI:Foundry:Endpoint"]}\n" +
    $"AzureAI:Speech:Region = {cfg["AzureAI:Speech:Region"]}\n\n" +
    $"Last TTS: {(tts is null ? "(none)" : $"{tts.Timestamp:u} status={tts.Status} reqB={tts.RequestBytes} respB={tts.ResponseSize} durMs={tts.DurationMs:F0} note={tts.Note}")}\n" +
    $"Last STT: {(stt is null ? "(none)" : $"{stt.Timestamp:u} status={stt.Status} reqB={stt.RequestBytes} respChars={stt.ResponseSize} durMs={stt.DurationMs:F0} note={stt.Note}")}\n\n" +
    "Metrics:\n" +
    " - speech_tts_duration_ms\n" +
    " - speech_stt_duration_ms\n" +
    " - speech_tts_request_bytes\n" +
    " - speech_tts_response_bytes\n" +
    " - speech_stt_request_bytes\n" +
    " - speech_stt_response_chars\n" +
    " - speech_tts_errors\n" +
    " - speech_stt_errors\n";
    return Results.Text(report, "text/plain");
});

app.MapDefaultEndpoints();

app.Run();

// small helper to avoid null checks for feature-setting
internal static class FeatureExt
{
    public static void Let<T>(this T? obj, Action<T> action) where T : class
    {
        if (obj is not null) action(obj);
    }
}