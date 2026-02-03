// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Common.Services;
using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Framework.Hooks;
using Agent.Runtime.Hooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Extensions;

/// <summary>
/// Extension methods for registering hook services in the DI container.
/// </summary>
public static class HookServiceCollectionExtensions
{
    /// <summary>
    /// Registers hook infrastructure services (HookManager, PromptHookExecutor, CommandHookExecutor).
    /// Call this after registering IChatClientProvider and ISessionPoolService.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHooks(this IServiceCollection services)
    {
        services.AddSingleton<ISandboxPaths, LocalSandboxPaths>();

        // Register hook file tools for transcript handling
        services.AddSingleton<IHookFileTools>(sp =>
        {
            var sandboxPaths = sp.GetRequiredService<ISandboxPaths>();
            var logger = sp.GetRequiredService<ILogger<HookFileTools>>();
            return new HookFileTools(sandboxPaths, logger);
        });

        // Register the prompt hook executor
        services.AddSingleton<IHookExecutor>(sp =>
        {
            var chatClientProvider = sp.GetRequiredService<IChatClientProvider>();
            var hookFileTools = sp.GetRequiredService<IHookFileTools>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = sp.GetRequiredService<ILogger<PromptHookExecutor>>();
            return new PromptHookExecutor(chatClientProvider, hookFileTools, loggerFactory, logger);
        });

        // Register the command hook executor
        services.AddSingleton<IHookExecutor>(sp =>
        {
            var sessionPoolService = sp.GetRequiredService<ISessionPoolService>();
            var hostEnvironment = sp.GetRequiredService<IHostEnvironment>();
            var logger = sp.GetRequiredService<ILogger<CommandHookExecutor>>();
            return new CommandHookExecutor(sessionPoolService, hostEnvironment, logger);
        });

        // Register the hook manager
        services.AddSingleton(sp =>
        {
            var executors = sp.GetServices<IHookExecutor>();
            var logger = sp.GetRequiredService<ILogger<HookManager>>();
            return new HookManager(executors, logger);
        });

        return services;
    }
}
