using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.

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

app.UseAuthorization();

app.MapControllers();

// Fallback to index.html for client-side routing
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(clientDistPath)
});

app.Run();
