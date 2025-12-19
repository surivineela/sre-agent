using Session.Identity;
using Session.Identity.Attributes;
using Session.Proxy;
using Session.Proxy.Configuration;
using Session.Proxy.Services;

// Check if running in test client mode
if (args.Length > 0 && args[0] == "TestClient")
{
    await TestClient.Run(args.Skip(1).ToArray());
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
                     .AddEnvironmentVariables();

var port = builder.Configuration.GetValue<int>("SessionProxy:Port", 5000);
var identityProviderSettings = builder.Configuration.GetSection("IdentityProvider").Get<IdentityProviderSettings>() ?? new IdentityProviderSettings();
var runIdentityProviderSidecar = identityProviderSettings.RunIdentityProviderSidecar;

builder.WebHost.UseUrls($"http://*:{port}");

if (runIdentityProviderSidecar)
{
    // Sidecar mode: only host Proxy controllers, identity provider runs separately
    builder.Services.AddControllersForMode(SessionMode.Proxy);
}
else
{
    // Integrated mode: host both Proxy and IdentityProvider controllers
    // Override the BaseUrl to point to this process
    identityProviderSettings.BaseUrl = $"http://localhost:{port}";
    builder.Services.AddControllersForMode(
        SessionMode.Proxy | SessionMode.IdentityProvider,
        typeof(IdentityProviderExtensions).Assembly);
    builder.Services.AddIdentityProviderServices();
}

builder.Services.AddHttpClient("IdentityProvider");
builder.Services.AddSingleton(identityProviderSettings);
builder.Services.AddSingleton<IdentityProviderClient>();
builder.Services.AddScoped<IShellService, ShellService>();
builder.Services.AddSingleton<McpProxyService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Configure WebSocket options
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};

app.UseWebSockets(webSocketOptions);
app.UseRouting();

if (runIdentityProviderSidecar)
{
    app.MapGet("/", () => "Session Proxy Server is running.");
}
else
{
    app.MapGet("/", () => "Session Proxy Server is running (with integrated Identity Provider).");
    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
}

app.MapControllers();

app.Run();
