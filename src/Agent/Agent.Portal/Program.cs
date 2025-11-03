using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Add Microsoft Identity authentication with token acquisition for downstream APIs
var initialScopes = builder.Configuration["AzureAd:Scopes"]?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

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

builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(options =>
    {
        builder.Configuration.Bind("AzureAd", options);

        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                // If using Vite dev server, override the redirect URI
                // Needed because the auth redirect needs to go to the same URL as the original request.
                // When we're using vite, the port is different than the backend.
                if (!string.IsNullOrEmpty(viteDevServerUrl))
                {
                    var viteRedirectUri = new Uri(new Uri(viteDevServerUrl), "/signin-oidc").ToString();
                    context.ProtocolMessage.RedirectUri = viteRedirectUri;
                }

                // Add prompt parameter if needed for consent
                if (context.Properties.Items.TryGetValue("prompt", out var prompt))
                {
                    context.ProtocolMessage.Prompt = prompt;
                }

                // Add tenant hint for tenant switching
                if (context.Properties.Items.TryGetValue("tenant_hint", out var tenantHint) && !string.IsNullOrEmpty(tenantHint))
                {
                    // Replace the tenant in the authority URL
                    // The IssuerAddress format is: https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize
                    var issuerUri = new Uri(context.ProtocolMessage.IssuerAddress);
                    var pathSegments = issuerUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                    // Replace the tenant (second segment after domain) with the target tenant
                    if (pathSegments.Length > 0)
                    {
                        pathSegments[0] = tenantHint;
                        var newPath = "/" + string.Join("/", pathSegments);
                        var newIssuerAddress = $"{issuerUri.Scheme}://{issuerUri.Host}{newPath}{issuerUri.Query}";
                        context.ProtocolMessage.IssuerAddress = newIssuerAddress;
                    }
                }

                return Task.CompletedTask;
            },
            OnRedirectToIdentityProviderForSignOut = context =>
            {
                // If using Vite dev server, override the redirect URI
                // Needed because the auth redirect needs to go to the same URL as the original request.
                // When we're using vite, the port is different than the backend.
                if (!string.IsNullOrEmpty(viteDevServerUrl))
                {
                    var vitePostLogoutRedirectUri = new Uri(new Uri(viteDevServerUrl), "/signout-callback-oidc").ToString();
                    context.ProtocolMessage.PostLogoutRedirectUri = vitePostLogoutRedirectUri;
                }
                return Task.CompletedTask;
            }
        };
    })
    .EnableTokenAcquisitionToCallDownstreamApi(initialScopes)
    .AddInMemoryTokenCaches();

// Register token providers
builder.Services.AddScoped<Agent.Portal.Services.ITokenProvider, Agent.Portal.Services.ArmTokenProvider>();
builder.Services.AddScoped<Agent.Portal.Services.ITokenProvider, Agent.Portal.Services.GraphTokenProvider>();
builder.Services.AddScoped<Agent.Portal.Services.ITokenProvider, Agent.Portal.Services.SreAgentTokenProvider>();
builder.Services.AddScoped<Agent.Portal.Services.ITokenProvider, Agent.Portal.Services.AppInsightsTokenProvider>();
builder.Services.AddScoped<Agent.Portal.Services.ITokenProviderFactory, Agent.Portal.Services.TokenProviderFactory>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.

// Use forwarded headers if using Vite dev server (must be first!)
// This is needed for authentication when using the vite server as the FE
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Fallback to index.html for client-side routing (but not for API routes)
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(clientDistPath)
}).AllowAnonymous();

app.Run();
