using Session.Identity;
using Session.Identity.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
                     .AddEnvironmentVariables();

var identityProviderSettings = builder.Configuration.GetSection("IdentityProvider").Get<IdentityProviderSettings>() ?? new IdentityProviderSettings();
builder.WebHost.UseUrls(identityProviderSettings.BaseUrl);

builder.Services.AddControllers();
builder.Services.AddIdentityProviderServices();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseRouting();
app.MapIdentityProviderEndpoints();
app.MapControllers();

app.Run();
