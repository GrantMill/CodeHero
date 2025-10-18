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

app.MapDefaultEndpoints();

app.Run();
