// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Web.Authorization;
using Agent.Web.Controllers.v2;
using Agent.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agent.Cli.Tests.E2E.MockBackend;

/// <summary>
/// WebApplicationFactory for creating an in-memory test server with API controllers.
/// This avoids all the complex dependencies from Agent.Web.Program by creating a minimal web server.
/// Shared across all E2E tests with data isolation via Reset() called per test.
/// </summary>
public class MockWebApplicationFactory : WebApplicationFactory<MockWebApplicationFactory.MinimalProgram>
{
    public MockExtendedAgentApiService MockService { get; } = new MockExtendedAgentApiService();
    public MockIncidentFilterApiService MockIncidentFilterService { get; } = new MockIncidentFilterApiService();

    protected override IHostBuilder? CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureServices(services =>
                {
                    // Add mock services
                    services.AddSingleton<IExtendedAgentApiService>(MockService);
                    services.AddSingleton<IIncidentFilterApiService>(MockIncidentFilterService);

                    // Add MVC with only the ExtendedAgentApiController
                    services.AddControllers()
                        .AddApplicationPart(typeof(ExtendedAgentApiController).Assembly)
                        .AddJsonOptions(options =>
                        {
                            // Match Agent.Web JSON configuration
                            options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                        });

                    // Add authorization with custom policy provider and handler that bypasses all checks
                    services.AddSingleton<IAuthorizationPolicyProvider, ArmOperationPolicyProvider>();
                    services.AddSingleton<IAuthorizationHandler, MockAuthorizationHandler>();
                    services.AddAuthorization();
                });

                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                    });
                });
            });
    }

    /// <summary>
    /// Minimal marker class - WebApplicationFactory requires a type parameter but we override CreateHostBuilder
    /// </summary>
    public class MinimalProgram
    {
    }

    /// <summary>
    /// Authorization handler that allows all ARM operations for testing
    /// </summary>
    private class MockAuthorizationHandler : AuthorizationHandler<ArmOperationRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ArmOperationRequirement requirement)
        {
            // Always succeed for any ARM operation in tests
            context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Reset all mock data between tests
    /// </summary>
    public void Reset()
    {
        MockService.Clear();
        MockIncidentFilterService.Clear();
    }
}

