using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.Plugins;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Plugins.Interfaces;
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
