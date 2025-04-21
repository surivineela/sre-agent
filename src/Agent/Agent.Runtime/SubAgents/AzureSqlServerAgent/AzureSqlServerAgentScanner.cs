using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.AzureSqlServerAgent
{
    public class AzureSqlServerScanner : SimpleResourceSubAgentScannerBase<AzureSqlServerAgent, AzureSqlServerAgentInput, AzureSqlServerActivity, AzureSqlServerAgentActivityInput>
    {
        public AzureSqlServerScanner(
            DurableTaskClient durableTaskClient,
            IThreadRepository threadRepository,
            AzureSqlServerAgentFactory AzureSqlServerAgentFactory,
            ILogger<AzureSqlServerAgent> logger,
            IAgentInboundCommunicationService agentInboundCommunicationService,
            IGraphDatabaseClient graphDatabaseClient,
            ArmHelper armHelper)
            : base(durableTaskClient, AzureSqlServerAgentFactory, logger, agentInboundCommunicationService, graphDatabaseClient, armHelper)
        {

        }

        protected override TimeSpan RunInterval => TimeSpan.FromMinutes(1);

        protected override string MessageWhenFoundResourcesInViolation => """
                    Hi there! I found AzureSqlServer's that does not have Azure Entra authentication only enabled. 
                    Preparing details...  
                    """;

        protected override AzureSqlServerAgentActivityInput GenerateActivityInput(IEnumerable<SimpleResourceSubAgentResourceInformation> Resources)
        {
            return new AzureSqlServerAgentActivityInput(
                    AzureSqlServerSetLocalAuthSupport: FeatureState.Disabled,
                    Resources.ToList()
            );
        }

        protected override async Task<ICollection<SimpleResourceSubAgentResourceInformation>> GetResourcesInViolationAsync()
        {
            var queryResults = await _graphDatabaseClient.Query("g.V().has('resourceType', 'microsoft.sql/servers').values('resourceId')");
            var resources = queryResults.Select(x => (string)x).OrderBy(resourceId => resourceId.Split("/").Last()).ToList();
            
            var azureSqlServerSettings = await _armHelper.GetAzureSqlServerSettings(resources);
            return azureSqlServerSettings.Where(x => x.IsAzureADOnlyAuthenticationEnabled == false)
                .Select(x => new SimpleResourceSubAgentResourceInformation(x.ResourceId, x.Name, x.Location))
                .ToList();
        }
    }
}
