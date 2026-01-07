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

builder.WebHost.UseUrls($"http://*:{port}");

builder.Services.AddControllers();

builder.Services.AddHttpClient("IdentityProvider");
builder.Services.AddHttpClient("PythonProxy")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        ConnectTimeout = TimeSpan.FromSeconds(10),
    })
    .ConfigureHttpClient(client =>
    {
        // Disable client-level timeout; use per-request timeout instead
        client.Timeout = Timeout.InfiniteTimeSpan;
    });
builder.Services.Configure<IdentityProviderSettings>(builder.Configuration.GetSection("IdentityProvider"));
builder.Services.AddSingleton<IdentityProviderClient>();
builder.Services.AddScoped<IShellService, ShellService>();
builder.Services.AddSingleton<McpProxyService>();
builder.Services.AddSingleton<PythonProxyService>();

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

app.MapGet("/", () => "Session Proxy Server is running.");

app.MapControllers();

// Fallback endpoint to proxy unmatched requests to another service
app.MapFallback(async (HttpContext context, PythonProxyService proxyService, CancellationToken cancellationToken) =>
{
    try
    {
        await proxyService.ForwardRequestAsync(context, cancellationToken);
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = StatusCodes.Status502BadGateway;
        await context.Response.WriteAsJsonAsync(new { error = "Failed to proxy request", message = ex.Message }, cancellationToken);
    }
});

app.Run();
