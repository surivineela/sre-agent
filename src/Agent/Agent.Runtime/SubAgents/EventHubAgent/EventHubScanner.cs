using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.EventHubAgent
{
    public class EventHubScanner : SimpleResourceSubAgentScannerBase<EventHubAgent, EventHubAgentInput, EventHubAgentActivity, EventHubAgentActivityInput>
    {
        public EventHubScanner(
            DurableTaskClient durableTaskClient,
            IThreadRepository threadRepository,
            EventHubAgentFactory EventHubAgentFactory,
            ILogger<EventHubAgent> logger,
            IAgentInboundCommunicationService agentInboundCommunicationService,
            IGraphDatabaseClient graphDatabaseClient,
            ArmHelper armHelper)
            : base(durableTaskClient, EventHubAgentFactory, logger, agentInboundCommunicationService, graphDatabaseClient, armHelper)
        {

        }

        protected override TimeSpan RunInterval => TimeSpan.FromMinutes(1);

        protected override string MessageWhenFoundResourcesInViolation => """
                    Hi there! I found Event Hubs that have keys enabled. 
                    Preparing details...  
                    """;

        protected override EventHubAgentActivityInput GenerateActivityInput(IEnumerable<SimpleResourceSubAgentResourceInformation> Resources)
        {
            return new EventHubAgentActivityInput(
                    EventHubSetLocalAuthSupport: FeatureState.Disabled,
                    Resources.ToList()
            );
        }

        protected override async Task<ICollection<SimpleResourceSubAgentResourceInformation>> GetResourcesInViolationAsync()
        {
            var queryResults = await _graphDatabaseClient.Query("g.V().has('resourceType', 'microsoft.eventhub/namespaces').values('resourceId')");
            var resources = queryResults.Select(x => (string)x).OrderBy(resourceId => resourceId.Split("/").Last()).ToList();
            
            var eventHubStatuses = await _armHelper.GetEventHubSettings(resources);
            return eventHubStatuses.Where(x => x.LocalAuthEnabled == true)
                .Select(x => new SimpleResourceSubAgentResourceInformation(x.ResourceId, x.Name, x.Location))
                .ToList();
        }
    }
}
