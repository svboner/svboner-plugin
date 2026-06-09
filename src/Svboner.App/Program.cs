using Svboner.Core.Buttplug;
using Svboner.Core.Config;
using Svboner.Core.Phd2;
using Svboner.Core.Services;
using Svboner.App;
using Svboner.App.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ── Core services ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ConfigStore>();
builder.Services.AddSingleton<Phd2Client>();
builder.Services.AddSingleton<DeviceController>();
builder.Services.AddSingleton<SvbonerOrchestrator>();

// ── Web layer ─────────────────────────────────────────────────────────────────
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter(
            System.Text.Json.JsonNamingPolicy.CamelCase));
});
builder.Services.AddSignalR().AddJsonProtocol(o =>
{
    o.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.PayloadSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter(
            System.Text.Json.JsonNamingPolicy.CamelCase));
});

// ── Background service that starts the orchestrator and broadcasts status ─────
builder.Services.AddHostedService<OrchestratorBroadcaster>();

// ── Read port from config (config store not yet built here, use default 8787) ─
builder.WebHost.UseUrls("http://localhost:8787");

var app = builder.Build();

// On startup, the ConfigStore might have a different port saved — honour it.
var configStore = app.Services.GetRequiredService<ConfigStore>();
var webPort = configStore.Get().Global.WebPort;
if (webPort != 8787)
{
    // Can't change the port after WebApplication is built, so just log a note.
    app.Logger.LogWarning(
        "Configured web port {Port} differs from the default 8787. " +
        "Edit appsettings or rebuild to apply a custom port.", webPort);
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapHub<StatusHub>("/hub");

// Open browser automatically.
app.Lifetime.ApplicationStarted.Register(() =>
{
    var url = $"http://localhost:{webPort}";
    try
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName  = url,
            UseShellExecute = true
        });
    }
    catch { /* non-fatal */ }

    app.Logger.LogInformation("SVBONER running at {Url}", url);
});

await app.RunAsync();
