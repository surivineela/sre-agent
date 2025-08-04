// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Runtime.AgentTasks.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agent.Runtime.AgentTasks;

public static class AgentTaskHostBuilderExtensions
{
    public static IHostApplicationBuilder AddAgentTaskService(this IHostApplicationBuilder hostBuilder)
    {
        hostBuilder.Services.AddTransient<AgentTaskPluginDefinition>();

        hostBuilder.Services.AddSingleton<IncidentInvestigationTaskHandler>();

        hostBuilder.Services.AddSingleton<IReadOnlyDictionary<AgentTaskType, IAgentTaskHandler>>(sp =>
        {
            return new Dictionary<AgentTaskType, IAgentTaskHandler>
            {
                { AgentTaskType.IncidentInvestigation, sp.GetRequiredService<IncidentInvestigationTaskHandler>() }
            };
        });

        hostBuilder.Services.AddSingleton<AgentTaskHandlerFactory>();

        hostBuilder.Services.AddSingleton<AgentTaskService>();
        hostBuilder.Services.AddHostedService(sp => sp.GetRequiredService<AgentTaskService>());

        return hostBuilder;
    }
}
