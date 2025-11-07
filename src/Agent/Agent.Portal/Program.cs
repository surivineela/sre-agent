using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Check if we're using Vite dev server (set via VITE_DEV_SERVER_URL environment variable in launchSettings)
var viteDevServerUrl = Environment.GetEnvironmentVariable("VITE_DEV_SERVER_URL");

// Configure forwarded headers if using Vite dev server
if (!string.IsNullOrEmpty(viteDevServerUrl))
{
    var viteUri = new Uri(viteDevServerUrl);
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();
    });
}

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.

// Use forwarded headers if using Vite dev server (must be first!)
if (!string.IsNullOrEmpty(viteDevServerUrl))
{
    app.UseForwardedHeaders();
}


app.UseHttpsRedirection();

// Serve static files from Client/dist folder
var clientDistPath = Path.Combine(app.Environment.ContentRootPath, "Client", "dist");
if (Directory.Exists(clientDistPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(clientDistPath),
        RequestPath = ""
    });
}
else
{
    app.Logger.LogWarning("Client dist folder not found at: {Path}. Run 'npm run build' in the Client folder.", clientDistPath);
}

app.MapControllers();

// Fallback to index.html for client-side routing (but not for API routes)
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(clientDistPath)
});

app.Run();
