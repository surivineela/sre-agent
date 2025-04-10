using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.CosmosDbAgent
{
    public class CosmosDbScanner : SimpleResourceSubAgentScannerBase<CosmosDbAgent, CosmosDbAgentInput, CosmosDbAgentActivity, CosmosDbAgentActivityInput>
    {
        public CosmosDbScanner(
            DurableTaskClient durableTaskClient,
            IThreadRepository threadRepository,
            CosmosDbAgentFactory CosmosDbAgentFactory,
            ILogger<CosmosDbAgent> logger,
            IAgentInboundCommunicationService agentInboundCommunicationService,
            IGraphDatabaseClient graphDatabaseClient,
            ArmHelper armHelper)
            : base(durableTaskClient, CosmosDbAgentFactory, logger, agentInboundCommunicationService, graphDatabaseClient, armHelper)
        {

        }

        protected override TimeSpan RunInterval => TimeSpan.FromMinutes(1);

        protected override string MessageWhenFoundResourcesInViolation => """
                    Hi there! I found CosmosDb's that have keys enabled. 
                    Preparing details...  
                    """;

        protected override CosmosDbAgentActivityInput GenerateActivityInput(IEnumerable<SimpleResourceSubAgentResourceInformation> Resources)
        {
            return new CosmosDbAgentActivityInput(
                    CosmosDbSetLocalAuthSupport: FeatureState.Disabled,
                    Resources.ToList()
            );
        }

        protected override async Task<ICollection<SimpleResourceSubAgentResourceInformation>> GetResourcesInViolationAsync()
        {
            var queryResults = await _graphDatabaseClient.Query("g.V().has('resourceType', 'microsoft.documentdb/databaseaccounts').values('resourceId')");
            var resources = queryResults.Select(x => (string)x).OrderBy(resourceId => resourceId.Split("/").Last()).ToList();
            
            var cosmosDbSettings = await _armHelper.GetCosmosDbSettings(resources);
            return cosmosDbSettings.Where(x => x.LocalAuthEnabled == false)
                .Select(x => new SimpleResourceSubAgentResourceInformation(x.ResourceId, x.Name, x.Location))
                .ToList();
        }
    }
}
