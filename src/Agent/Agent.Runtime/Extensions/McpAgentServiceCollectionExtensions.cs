// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Services.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Runtime.Extensions;

/// <summary>
/// Extension methods for registering MCP agent services.
/// </summary>
public static class McpAgentServiceCollectionExtensions
{
    /// <summary>
    /// Adds MCP agent services to the service collection.
    /// </summary>
    public static IServiceCollection AddMcpAgentServices(this IServiceCollection services)
    {
        // Register IMcpAuthenticationService
        services.AddSingleton<IMcpAuthenticationService>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<McpAuthenticationService>();

            return new McpAuthenticationService(logger);
        });

        // Register ISessionTransportFactory
        services.AddSingleton<ISessionTransportFactory>(sp =>
        {
            var coreAuthService = sp.GetRequiredService<IAuthenticationService>();
            var sessionPoolService = sp.GetRequiredService<ISessionPoolService>();
            var azureSettings = sp.GetRequiredService<AzureSettings>();
            var hostEnvironment = sp.GetRequiredService<IHostEnvironment>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<SessionTransportFactory>();

            return new SessionTransportFactory(coreAuthService, sessionPoolService, azureSettings.SessionPool, hostEnvironment, logger);
        });

        // Register IMcpConnectionEventManager
        services.AddSingleton<IMcpConnectionEventManager>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var backend = sp.GetRequiredService<IMcpConnectable>();
            var authService = sp.GetRequiredService<IMcpAuthenticationService>();
            var coreAuthService = sp.GetRequiredService<IAuthenticationService>();
            var sessionTransportFactory = sp.GetRequiredService<ISessionTransportFactory>();
            var mcpSettings = sp.GetRequiredService<IOptions<MCPSettings>>();
            var logger = loggerFactory.CreateLogger<McpConnectionEventManager>();

            return new McpConnectionEventManager(backend, authService, coreAuthService, sessionTransportFactory, mcpSettings, logger);
        });

        // Register IMcpConnectionHealthService
        services.AddSingleton<IMcpConnectionHealthService>(sp =>
        {
            var connectionManager = sp.GetRequiredService<IMcpConnectionEventManager>();
            var logger = sp.GetRequiredService<ILogger<McpConnectionHealthService>>();

            return new McpConnectionHealthService(connectionManager, logger);
        });

        // Register McpAgentManagementService as hosted service
        services.AddHostedService<McpAgentManagementService>();

        return services;
    }
}
