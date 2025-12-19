using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Session.Identity.Attributes;
using Session.Identity.Services;

namespace Session.Identity;

/// <summary>
/// Extension methods for registering Session.Identity services and endpoints.
/// </summary>
public static class IdentityProviderExtensions
{
    /// <summary>
    /// Adds controllers filtered by the specified session mode.
    /// Controllers are filtered based on the SessionModeAttribute.
    /// </summary>
    public static IMvcBuilder AddControllersForMode(this IServiceCollection services, SessionMode mode, params Assembly[] additionalAssemblies)
    {
        return services.AddControllers()
            .ConfigureApplicationPartManager(manager =>
            {
                foreach (var assembly in additionalAssemblies)
                {
                    manager.ApplicationParts.Add(new AssemblyPart(assembly));
                }
                manager.FeatureProviders.Add(new SessionModeControllerFeatureProvider(mode));
            });
    }

    /// <summary>
    /// Adds Session.Identity services to the service collection.
    /// </summary>
    public static IServiceCollection AddIdentityProviderServices(this IServiceCollection services)
    {
        services.AddSingleton<IManagedIdentityService, ManagedIdentityService>();
        services.AddSingleton<StaticTokenService>();
        services.AddSingleton<ManagedIdentityTokenService>();
        services.AddSingleton<ITokenService, CompositeTokenService>();
        return services;
    }

    /// <summary>
    /// Maps the Session.Identity endpoints to the application.
    /// </summary>
    public static WebApplication MapIdentityProviderEndpoints(this WebApplication app)
    {
        app.MapGet("/", () => "Session Identity Provider is running.");
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
        return app;
    }
}
