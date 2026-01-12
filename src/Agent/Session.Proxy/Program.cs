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

builder.Services.Configure<IdentityProviderSettings>(builder.Configuration.GetSection("IdentityProvider"));
builder.Services.AddSingleton<IdentityProviderClient>();
builder.Services.AddScoped<IShellService, ShellService>();
builder.Services.AddSingleton<McpProxyService>();

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

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

// YARP reverse proxy for unmatched requests
app.MapReverseProxy();

app.Run();
