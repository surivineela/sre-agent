// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.AgentInteraction;
using Agent.Plugins.Definitions;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Hosting;

#pragma warning disable IDE0130 // extensios should be in the same namespace as the containing type
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // extensios should be in the same namespace as the containing type

public static class FunctionsFirstPartyRegistrationExtensions
{
    public static void RegisterFunctionsFirstPartyTypes(this IHostApplicationBuilder builder)
    {
        // Register the AgentInteractionPlugin interface and implementation
        builder.Services.AddTransient<AgentInteractionPluginDefinition>();
        builder.Services.AddTransient<IAgentInteractionPlugin, AgentInteractionPlugin>();
    }
}
