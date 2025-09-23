using Session.Cli.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
                     .AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<ITokenService, StaticTokenService>();
builder.Services.AddScoped<IShellService, ShellService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseRouting();
app.MapControllers();

app.Run();
