using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.ServiceBusAgent
{
    public class ServiceBusScanner : SimpleResourceSubAgentScannerBase<ServiceBusAgent, ServiceBusAgentInput, ServiceBusAgentActivity, ServiceBusAgentActivityInput>
    {
        public ServiceBusScanner(
            DurableTaskClient durableTaskClient,
            IThreadRepository threadRepository,
            ServiceBusAgentFactory ServiceBusAgentFactory,
            ILogger<ServiceBusAgent> logger,
            IAgentInboundCommunicationService agentInboundCommunicationService,
            IGraphDatabaseClient graphDatabaseClient,
            ArmHelper armHelper)
            : base(durableTaskClient, ServiceBusAgentFactory, logger, agentInboundCommunicationService, graphDatabaseClient, armHelper)
        {

        }

        protected override TimeSpan RunInterval => TimeSpan.FromMinutes(1);

        protected override string MessageWhenFoundResourcesInViolation => """
                    Hi there! I found Service Bus that have keys enabled. 
                    Preparing details...  
                    """;

        protected override ServiceBusAgentActivityInput GenerateActivityInput(IEnumerable<SimpleResourceSubAgentResourceInformation> Resources)
        {
            return new ServiceBusAgentActivityInput(
                    ServiceBusSetLocalAuthSupport: FeatureState.Disabled,
                    Resources.ToList()
            );
        }

        protected override async Task<ICollection<SimpleResourceSubAgentResourceInformation>> GetResourcesInViolationAsync()
        {
            var queryResults = await _graphDatabaseClient.Query("g.V().has('resourceType', 'microsoft.servicebus/namespaces').values('resourceId')");
            var resources = queryResults.Select(x => (string)x).OrderBy(resourceId => resourceId.Split("/").Last()).ToList();

            var ServiceBusStatuses = await _armHelper.GetServiceBusSettings(resources);
            return ServiceBusStatuses.Where(x => x.LocalAuthEnabled == false)
                .Select(x => new SimpleResourceSubAgentResourceInformation(x.ResourceId, x.Name, x.Location))
                .ToList();
        }
    }
}
