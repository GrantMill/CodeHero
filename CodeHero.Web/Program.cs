using CodeHero.Web;
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
builder.Services.AddSingleton<ISpeechService, AzureSpeechService>();

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

app.MapRazorComponents<CodeHero.Web.Components.App>()
    .AddInteractiveServerRenderMode();

// Minimal TTS/STT endpoints (dev only)
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

app.MapDefaultEndpoints();

app.Run();
