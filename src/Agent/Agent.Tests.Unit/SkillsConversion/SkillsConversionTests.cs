// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Framework.Skills;
using Agent.Runtime;
using Agent.Runtime.Reasoning;
using Agent.Runtime.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Tests.Unit.SkillsConversion;

public class SkillsConversionTests
{
    private WebApplication _webapp;

    public SkillsConversionTests()
    {
        var builder = Web.Program.CreateWebApplicationBuilder(new WebApplicationOptions { EnvironmentName = "test" });
        builder.LoadAppSettings(isDevelopment: true);
        builder.ValidateAndRegisterAppSettings<AppSettings>();

        // re-register agent factory to ensure extensible agents are not loaded for the tests
        builder.Services.ReplaceAll<IAgentFactory<AgentContext>>(ServiceLifetime.Singleton, sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AgentFactory<AgentContext>>>();
            var toolFactory = sp.GetRequiredService<IToolFactory<AgentContext>>();
            var chatClientProvider = sp.GetRequiredService<IChatClientProvider>();
            var hostEnvironment = sp.GetRequiredService<IHostEnvironment>();
            var modeConfigurator = sp.GetRequiredService<IAgentModeConfigurator<AgentContext>>();

            return new AgentFactory<AgentContext>(
                logger: logger,
                toolFactory: toolFactory,
                chatClientProvider: chatClientProvider,
                assembliesToScan: AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    .Where(assembly => assembly.GetName()?.Name?.StartsWith("Agent.Runtime") == true),
                modeConfigurator: modeConfigurator,
                agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "AgentsV2"),
                commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "CommonPrompts"),
                commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "CommonTools"),
                promptEnders: [Agent.Core.Constants.SREAgentFinalInstructions],
                defaultOutputType: typeof(DefaultAgentOutput),
                enableHandoffReasoning: true,
                gpt5Enabled: true,
                agentMemoryRetrievalEnabled: true,
                scheduledTasksEnabled: true,
                dynamicAgentDescriptors: [
                    () =>
                    {
                        var dynamicIncidentManagementAgent = sp.GetRequiredService<DynamicIncidentManagementAgent>();
                        return dynamicIncidentManagementAgent.GetIncidentManagementAgentDescriptor();
                    }
                ]);
        });

        _webapp = builder.Build();
    }

    private Task InitializeAsync()
    {
        var asyncInitializerService = _webapp.Services.GetRequiredService<AsyncInitializerService>() as IHostedService;
        return asyncInitializerService.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AllTopLevelAgentsAreConvertedToSkills()
    {
        await InitializeAsync();

        var agentFactory = _webapp.Services.GetRequiredService<IAgentFactory<AgentContext>>();
        var skillsRegistry = _webapp.Services.GetRequiredService<ISkillRegistry>();

        var metaAgent = agentFactory.GetAgent("meta_agent");

        if (metaAgent == null)
        {
            return;
        }

        IReadOnlyList<string> exemptAgents = [
            "resource_discovery_agent", // folded into meta_agent directly
            "incident_management_agent" // dynamically loaded agent
        ];

        List<string> handoffAgentNames = [.. metaAgent.Handoffs.Select(h => h.AgentName)];
        handoffAgentNames.RemoveAll(a => exemptAgents.Contains(a));

        foreach (var agentName in handoffAgentNames)
        {
            var skillName = AgentToSkillService.GetAgentToSkillName(agentName);
            var skill = skillsRegistry.GetSkillByName(skillName, includeSystemSkills: true);
            // Provide a helpful failure message including available skills metadata
            Assert.True(skill != null,
                $"""
                Found agent {agentName} in Meta Agent handoffs that has not been converted to a skill.
                Expected to find skill with name '{skillName}' in the directory src\Agent\Agent.Runtime\Skills\
                Run the following command to convert the agent to a skill (from directory src\Agent\):
                ```
                dotnet run --project .\Agent.Cmd\Agent.Cmd.csproj -- convert-to-skill --agent-name {agentName} --output-directory .\Agent.Runtime\Skills\
                ```
                Then review the generated skill content to ensure quality and accuracy. PLEASE NOTE: the resource_discovery_agent is folded into the meta_agent prompt now,
                so scan the new skill content accordingly to make sure it doesn't reference resource_discovery anymore.
                """);
        }
    }
}
