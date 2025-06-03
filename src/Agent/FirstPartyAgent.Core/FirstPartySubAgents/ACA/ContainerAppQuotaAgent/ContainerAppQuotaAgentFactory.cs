// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Linq.Expressions;
using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent;
using FirstPartyAgent.Core.Plugins;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppQuotaAgent
{
    public class ContainerAppQuotaAgentFactory 
    {
        private readonly IToolsRepository _toolsRegistry;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadOrchestrationManager _mappingManager;
        private readonly IReadOnlyList<string> _toolSignatures;
        public const string OrchestrationInstanceIdPrefix = nameof(ContainerAppQuotaAgent);

        public ContainerAppQuotaAgentFactory(
            IContainerAppIcMPlugin containerAppIcMPlugin,
            IContainerAppsPlugin containerAppsPlugin,
            IContainerAppQuotaPlugin containerAppQuotaPlugin,
            IAzureDocSearchPlugin azureDocSearchPlugin,
            IToolsRepository toolsRepository,
            IThreadOrchestrationManager mappingManager,
            DurableTaskClient durableTaskClient
            )
            
        {
            _toolsRegistry = toolsRepository;
            var toolSignatures = new List<string>();

            var containerAppIcMPluginDefinition = new ContainerAppIcMPluginDefinition(containerAppIcMPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.GetIncidentInfo));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.AddDiscussionEntry));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.ResolveIncident));

            var containerAppsPluginDefinition = new ContainerAppsPluginDefinition(containerAppsPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppsPluginDefinition.GetSubscriptionDetail));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppsPluginDefinition.GetSubscriptionUsage));

            var containerAppQuotaPluginDefinition = new ContainerAppQuotaPluginDefinition(containerAppQuotaPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppQuotaPluginDefinition.ValidateQuotaRequest));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppQuotaPluginDefinition.GetSubscriptionQuota));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppQuotaPluginDefinition.SetSubscriptionQuota));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppQuotaPluginDefinition.GetEnvironmentQuota));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppQuotaPluginDefinition.SetEnvironmentQuota));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppQuotaPluginDefinition.GetEnvironmentQuotaOperationResult));

            var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.Wait));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));
            var searchPluginDefintion = new ContainerAppDocumentSearchPluginDefinition(azureDocSearchPlugin);

            toolSignatures.Add(_toolsRegistry.GetSignature(() => searchPluginDefintion.SearchAzureContainerAppsDocumentation));
            _toolSignatures = toolSignatures;
            _durableTaskClient = durableTaskClient;
            _mappingManager = mappingManager;
        }

        public async Task<string> StartOrchestration(
            ContainerAppQuotaAgentActivityInput input,
            Guid threadId)
        {
            var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

            await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

            await _durableTaskClient.ScheduleNewContainerAppQuotaAgentInstanceAsync(
                new ContainerAppQuotaAgentInput(
                    Input: input,
                    ToolSignatures: _toolSignatures,
                    ThreadId: threadId),
                new StartOrchestrationOptions(InstanceId: instanceId));

            return instanceId;
        }

        public ContainerAppQuotaAgentActivityInput DeserializeInput(string serializedOrchestrationInput)
        {
            return JsonSerializer.Deserialize<ContainerAppQuotaAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
        }
    }
}
