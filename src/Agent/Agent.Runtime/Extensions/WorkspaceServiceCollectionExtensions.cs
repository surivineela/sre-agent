// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Plugins.Interface;
using Agent.Plugins.Services;
using Agent.Runtime.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Runtime.Extensions;

/// <summary>
/// Extension methods for registering workspace tools and services with the DI container.
/// </summary>
public static class WorkspaceServiceCollectionExtensions
{
    /// <summary>
    /// Adds workspace tools and related services to the service collection.
    /// Includes file operations, terminal execution, search, task management, and user question capabilities.
    /// </summary>
    public static IServiceCollection AddWorkspaceServices(this IServiceCollection services)
    {
        // Register the terminal session manager as singleton (manages terminal processes)
        services.AddSingleton<TerminalSessionManager>();

        // Register the plugin implementation as singleton
        // Todo list is keyed by ThreadContextAccessor.CurrentThreadId, so plugin can be shared
        services.AddSingleton<IWorkspaceToolsPlugin, WorkspaceToolsPlugin>();

        // Register the ambient context provider (injects workspace/environment context into prompts)
        services.AddSingleton<IAmbientContextProvider, WorkspaceAmbientContextProvider>();

        // Register the plugin definition for tool discovery
        services.AddTransient<WorkspacePluginDefinition>();

        // Register the user question service (handles interactive user prompts)
        services.AddSingleton<IUserQuestionService, UserQuestionService>();

        // Register the AskUserQuestion plugin definition
        services.AddTransient<AskUserQuestionPluginDefinition>();

        return services;
    }
}
