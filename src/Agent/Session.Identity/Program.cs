using Session.Identity;
using Session.Identity.Attributes;
using Session.Identity.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Session.Proxy currently project references Session.Identity for shared code, causing conflicted appsetting.json
// Uncomment after the reference is removed
// builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
//                      .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
//                      .AddEnvironmentVariables();

// var identityProviderSettings = builder.Configuration.GetSection("IdentityProvider").Get<IdentityProviderSettings>() ?? new IdentityProviderSettings();
// builder.WebHost.UseUrls(identityProviderSettings.BaseUrl);
var baseUrl = Environment.GetEnvironmentVariable("IdentityProvider__BaseUrl") ?? "http://localhost:12356";
builder.WebHost.UseUrls(baseUrl);

builder.Services.AddControllersForMode(SessionMode.IdentityProvider);
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
