// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Plugins.Implementation;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppCorednsAgent
{
    public sealed class ContainerAppCorednsAgentFactory
    {
        private readonly IToolsRepository _toolsRegistry;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadOrchestrationManager _mappingManager;
        private readonly IReadOnlyList<string> _toolSignatures;
        public const string OrchestrationInstanceIdPrefix = nameof(ContainerAppCorednsAgent);

        public ContainerAppCorednsAgentFactory(
        IMetricsPlugin metricsPlugin,
        ITimePlugin timePlugin,
        IContainerAppCorednsPlugin corednsPlugin,
        IContainerAppIcMPlugin containerAppIcMPlugin,
        IManagedClusterPlugin  managedClusterPlugin,
        IChartPlugin chartPlugin,
        IToolsRepository toolsRepository,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
        {
            _toolsRegistry = toolsRepository;
            var toolSignatures = new List<string>();

            var dnsPluginDefinition = new ContainerAppCorednsPluginDefinition(corednsPlugin, managedClusterPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => dnsPluginDefinition.GetCustomDNSServers));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => dnsPluginDefinition.GetUpstreamCustomDNSServerHealthStatus));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => dnsPluginDefinition.GetCoreDNSConfigReloadFailuresCount));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => dnsPluginDefinition.GetCoreDNSTotalDNSRequestCount));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => dnsPluginDefinition.GetCoreDNSForwardConcurrentRejectsCount));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => dnsPluginDefinition.GetAverageLatencyOfDNSResolutionRequests));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => dnsPluginDefinition.GetAverageLatencyOfCoreDNSKubernetesDNSProgramming));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => dnsPluginDefinition.GetAverageLatencyOfUpstreamDNSResolutionForwardRequests));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => dnsPluginDefinition.GetCorednsPodFailureEvents));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => dnsPluginDefinition.GetSwiftBootstrapAgentPodFailureEvents));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => dnsPluginDefinition.GetSwiftBootstrapAgentPodHealthStatus));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => dnsPluginDefinition.GetDNSConfigUpdateStatus));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => dnsPluginDefinition.CheckIfDNSServerFailedToResolveDot));

            var managedClusterPluginDefinition = new ManagedClusterPluginDefinition(managedClusterPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedClusterPluginDefinition.GetASIPageForManagedCluster));

            var containerAppIcMPluginDefinition = new ContainerAppIcMPluginDefinition(containerAppIcMPlugin);
            // READ operations
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.GetInitialInvestigationReportAsync));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.GetIssueInvestigationTimeRange));
            // WRITE operations
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.AddDiscussionEntry));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.WasAgentHelpfulInDebuggingIssueAsync));


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
            ContainerAppCorednsAgentActivityInput input,
            Guid threadId)
        {
            var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

            await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

            await _durableTaskClient.ScheduleNewContainerAppCorednsAgentInstanceAsync(
                new CorednsAgentInput(
                    Input: input,
                    ToolSignatures: _toolSignatures,
                    ThreadId: threadId),
                new StartOrchestrationOptions(InstanceId: instanceId));

            return instanceId;
        }

        public ContainerAppCorednsAgentActivityInput DeserializeInput(string serializedOrchestrationInput)
        {
            return JsonSerializer.Deserialize<CorednsAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
        }
    }
}
