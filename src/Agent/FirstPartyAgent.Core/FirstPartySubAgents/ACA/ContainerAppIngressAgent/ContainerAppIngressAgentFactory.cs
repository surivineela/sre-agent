// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Linq.Expressions;
using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Interface;
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

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppIngressAgent
{
    public class ContainerAppIngressAgentFactory 
    {
        private readonly IToolsRepository _toolsRegistry;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadOrchestrationManager _mappingManager;
        private readonly IReadOnlyList<string> _toolSignatures;
        public const string OrchestrationInstanceIdPrefix = nameof(ContainerAppIngressAgentInput);

        public ContainerAppIngressAgentFactory(
            IContainerAppEnvoyPlugin envoyPlugin,
            IToolsRepository toolsRepository,
            IChartPlugin chartPlugin,
            IThreadOrchestrationManager mappingManager,
            IAzureDocSearchPlugin azureDocSearchPlugin,
            DurableTaskClient durableTaskClient)
        {
            _toolsRegistry = toolsRepository;
            var toolSignatures = new List<string>();

            var envoyPluginDefinition = new ContainerAppEnvoyPluginDefinition(envoyPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetContainerAppManagedCluster));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetEnvoyPodLogs));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetEnvoyControllerLogs));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetEnvoyAccessRequestCountTimeSeries));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetEnvoyAccessLogs));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetSwiftNetworkingEvents));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetContainerAppAdminEvents));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetContainerAppStatus));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetEnvoyPodStatus));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetContainerAppPodStatus));

            var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.Wait));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

            var chartPluginDefinition = new ChartPluginDefinition(chartPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => chartPluginDefinition.PlotTimeSeriesData));

            var searchPluginDefintion = new ContainerAppDocumentSearchPluginDefinition(azureDocSearchPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => searchPluginDefintion.SearchAzureContainerAppsDocumentation));

            _toolSignatures = toolSignatures;
            _durableTaskClient = durableTaskClient;
            _mappingManager = mappingManager;
        }

        public async Task<string> StartOrchestration(
           ContainerAppIngressAgentActivityInput input,
           Guid threadId)
        {
            var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

            await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

            await _durableTaskClient.ScheduleNewContainerAppIngressAgentInstanceAsync(
                new ContainerAppIngressAgentInput(
                    Input: input,
                    ToolSignatures: _toolSignatures,
                    ThreadId: threadId),
                new StartOrchestrationOptions(InstanceId: instanceId));

            return instanceId;
        }

        public ContainerAppIngressAgentActivityInput DeserializeInput(string serializedOrchestrationInput)
        {
            return JsonSerializer.Deserialize<ContainerAppIngressAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
        }
    }
}
