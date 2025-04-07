using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.StorageAccountAgent
{
    public class StorageAccountScanner : SimpleResourceSubAgentScannerBase<StorageAccountAgent, StorageAccountAgentInput, StorageAccountAgentActivity, StorageAccountAgentActivityInput>
    {
        public StorageAccountScanner(
            DurableTaskClient durableTaskClient,
            IThreadRepository threadRepository,
            StorageAccountAgentFactory storageAccountAgentFactory,
            ILogger<StorageAccountAgent> logger,
            IAgentInboundCommunicationService agentInboundCommunicationService,
            IGraphDatabaseClient graphDatabaseClient,
            ArmHelper armHelper)
            : base(durableTaskClient, storageAccountAgentFactory, logger, agentInboundCommunicationService, graphDatabaseClient, armHelper)
        {

        }

        protected override TimeSpan RunInterval => TimeSpan.FromMinutes(1);

        protected override string MessageWhenFoundResourcesInViolation => """
                    Hi there! I found Storage Accounts that have storage keys enabled. 
                    Preparing details...  
                    """;

        protected override StorageAccountAgentActivityInput GenerateActivityInput(IEnumerable<SimpleResourceSubAgentResourceInformation> Resources)
        {
            return new StorageAccountAgentActivityInput(
                    KeyBasedAccessDesiredState: FeatureState.Disabled,
                    BlobPublicAccessDesiredState: FeatureState.Disabled,
                    Resources.ToList()
            );
        }

        protected override async Task<ICollection<SimpleResourceSubAgentResourceInformation>> GetResourcesInViolationAsync()
        {
            var queryResults = await _graphDatabaseClient.Query("g.V().has('resourceType', 'microsoft.storage/storageaccounts').values('resourceId')");
            var resources = queryResults.Select(x => (string)x).OrderBy(resourceId => resourceId.Split("/").Last()).ToList();
            
            var storageKeySettings = await _armHelper.GetStorageSettings(resources);
            return storageKeySettings.Where(x => x.StorageKeyEnabled == true || x.PublicContainersEnabled == true)
                .Select(x => new SimpleResourceSubAgentResourceInformation(x.ResourceId, x.Name, x.Location))
                .ToList();
        }
    }
}
