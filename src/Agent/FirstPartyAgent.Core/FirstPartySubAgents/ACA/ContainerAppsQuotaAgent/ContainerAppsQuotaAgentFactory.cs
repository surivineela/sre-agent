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
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppsQuotaAgent
{
    // [MENDATORY]
    public class ContainerAppsQuotaAgentFactory 
    {
        private readonly IToolsRepository _toolsRegistry;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadOrchestrationManager _mappingManager;
        private readonly IReadOnlyList<string> _toolSignatures;
        public const string OrchestrationInstanceIdPrefix = nameof(ContainerAppsQuotaAgent);

        public ContainerAppsQuotaAgentFactory(
            IIcmPlugin icmPlugin,
            IContainerAppsPlugin containerAppsPlugin,
            IContainerAppQuotaPlugin containerAppQuotaPlugin,
            IToolsRepository toolsRepository,
            IThreadOrchestrationManager mappingManager,
            DurableTaskClient durableTaskClient
            )
            
        {
            _toolsRegistry = toolsRepository;
            var toolSignatures = new List<string>();

            var icmPluginDefinition = new IcmPluginDefinition(icmPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => icmPluginDefinition.GetIncidentInfo));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => icmPluginDefinition.AddDiscussionEntry));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => icmPluginDefinition.ResolveIncident));

            var containerAppsPluginDefinition = new ContainerAppsPluginDefinition(containerAppsPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppsPluginDefinition.GetSubscriptionDetail));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppsPluginDefinition.GetSubscriptionUsage));

            var containerAppQuotaPluginDefinition = new ContainerAppQuotaPluginDefinition(containerAppQuotaPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppQuotaPluginDefinition.ValidateQuotaRequest));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppQuotaPluginDefinition.SetSubscriptionQuota));
            
            var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.Wait));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

            _toolSignatures = toolSignatures;
            _durableTaskClient = durableTaskClient;
            _mappingManager = mappingManager;
        }

        public async Task<string> StartOrchestration(
            ContainerAppsQuotaAgentActivityInput input,
            Guid threadId)
        {
            var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

            await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

            await _durableTaskClient.ScheduleNewContainerAppsQuotaAgentInstanceAsync(
                new ContainerAppsQuotaAgentInput(
                    Input: input,
                    ToolSignatures: _toolSignatures,
                    ThreadId: threadId),
                new StartOrchestrationOptions(InstanceId: instanceId));

            return instanceId;
        }

        public ContainerAppsQuotaAgentActivityInput DeserializeInput(string serializedOrchestrationInput)
        {
            return JsonSerializer.Deserialize<ContainerAppsQuotaAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
        }
    }
}
