using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.Plugins;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppCustomerMetricsAgent
{
    public sealed class ContainerAppCustomerMetricsAgentFactory
    {

        private readonly IToolsRepository _toolsRegistry;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadOrchestrationManager _mappingManager;
        private readonly IReadOnlyList<string> _toolSignatures;
        public const string OrchestrationInstanceIdPrefix = nameof(ContainerAppCustomerMetricsAgent);

        public ContainerAppCustomerMetricsAgentFactory(
        IContainerAppCustomerMetricsPlugin metricsAgentPlugin,
        IManagedClusterPlugin managedClusterPlugin,
        IContainerAppIcMPlugin containerAppIcMPlugin,
        IAzureDocSearchPlugin azureDocSearchPlugin,
        IToolsRepository toolsRepository,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
        {
            _toolsRegistry = toolsRepository;
            var toolSignatures = new List<string>();

            var remediationPluginDefinition = new ContainerAppCustomerMetricsPluginDefinition(metricsAgentPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.GetMetricsMdmCount));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.GetMdmPodHeartbeatMissedTimes));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.GetMissedMdmMetricTimes));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.GetBillingPodLeaderElection));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.GetContainerAppInfraLayer));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.GetVKPodLeaderElection));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.GetAKSKubeletRuntimeErrors));

            var containerAppIcMPluginDefinition = new ContainerAppIcMPluginDefinition(containerAppIcMPlugin);
            // READ operations
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.GetInitialInvestigationReportAsync));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.GetIssueInvestigationTimeRange));
            // WRITE operations
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.AddDiscussionEntry));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.WasAgentHelpfulInDebuggingIssueAsync));

            var managedClusterPluginDefinition = new ManagedClusterPluginDefinition(managedClusterPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedClusterPluginDefinition.GetAksClusterCcpNamespace));

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
            ContainerAppCustomerMetricsAgentActivityInput input,
            Guid threadId)
        {
            var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

            await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

            await _durableTaskClient.ScheduleNewContainerAppCustomerMetricsAgentInstanceAsync(
                new CustomerMetricsAgentInput(
                    Input: input,
                    ToolSignatures: _toolSignatures,
                    ThreadId: threadId),
                new StartOrchestrationOptions(InstanceId: instanceId));

            return instanceId;
        }

        public ContainerAppCustomerMetricsAgentActivityInput DeserializeInput(string serializedOrchestrationInput)
        {
            return JsonSerializer.Deserialize<CustomerMetricsAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
        }
    }
}
