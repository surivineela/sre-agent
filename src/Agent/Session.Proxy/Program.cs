using Session.Proxy;
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

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<ITokenService, StaticTokenService>();
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

app.MapGet("/", () => "Session Proxy Server is running.");
app.MapControllers();

app.Run();
