// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using Agent.Runtime.AgentTasks.Handlers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agent.Runtime.AgentTasks;

public static class AgentTaskHostBuilderExtensions
{
    public static IHostApplicationBuilder AddAgentTaskService(this IHostApplicationBuilder hostBuilder)
    {
        hostBuilder.Services.AddTransient<AgentTaskPluginDefinition>();

        hostBuilder.Services.AddTransient<IncidentInvestigationTaskHandler>();

        hostBuilder.Services.AddSingleton<AgentTaskHandlerFactory>();
        hostBuilder.Services.AddSingleton(provider =>
        {
            bool is1PAgent = Environment.GetEnvironmentVariable("AGENT_TYPE_NAME") == "ACAAgent";
            var chatClientProvider = provider.GetRequiredService<IChatClientProvider>();
            return new AgentTaskLocalStore([], chatClientProvider.EmbeddingModel);
        });

        hostBuilder.Services.AddSingleton<AgentTaskService>();
        hostBuilder.Services.AddHostedService(sp => sp.GetRequiredService<AgentTaskService>());

        return hostBuilder;
    }
}
