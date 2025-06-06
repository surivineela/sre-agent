using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace Agent.Runtime.Services
{
    public interface IIncidentHandlerManagementService
    {
        Task<List<IncidentHandlerDocument>> ListIncidentHandlers();
        Task<List<IncidentHandlerDocument>> QueryIncidentHandlers(List<string> keywords);
        Task<IncidentHandlerDocument?> GetIncidentHandler(string handlerId);
        Task<IncidentHandlerDocument> SaveIncidentHandler(IncidentHandlerDocument incidentHandlerDocument);
        Task<bool> DeleteIncidentHandler(string handlerId);
    }

    public class IncidentHandlerManagementService : IIncidentHandlerManagementService
    {
        private readonly Container _container;
        protected readonly string DocumentType = "IncidentHandler";

        public IncidentHandlerManagementService(CosmosClient cosmosClient, CosmosDBSettings cosmosDbSettings)
        {
            _container = cosmosClient.GetContainer(
                cosmosDbSettings.Docs.Database,
                AgentDataConfiguration.ThreadContainerName
            );
        }

        public async Task<List<IncidentHandlerDocument>> ListIncidentHandlers()
        {
            // Return all incident handlers
            var queryable = _container.GetItemLinqQueryable<IncidentHandlerDocument>(allowSynchronousQueryExecution: false)
                .Where(c => c.DocumentType == DocumentType && c.IsDeleted == false);

            var iterator = queryable.ToFeedIterator();
            var results = new List<IncidentHandlerDocument>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }
            return results;
        }

        public async Task<IncidentHandlerDocument?> GetIncidentHandler(string handlerId)
        {
            var queryable = _container.GetItemLinqQueryable<IncidentHandlerDocument>(allowSynchronousQueryExecution: false)
                .Where(c => c.DocumentType == DocumentType && c.Id == handlerId && c.IsDeleted == false)
                .Take(1);

            var iterator = queryable.ToFeedIterator();
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                return response.FirstOrDefault();
            }
            return null;
        }

        public async Task<IncidentHandlerDocument> SaveIncidentHandler(IncidentHandlerDocument document)
        {
            try
            {
                if (document == null)
                    throw new ArgumentNullException(nameof(document));
                var response = await _container.UpsertItemAsync(document, new PartitionKey(document.PartitionKey ?? document.Id));
                return response.Resource;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<bool> DeleteIncidentHandler(string handlerId)
        {
            // do a soft delete by setting isDeleted to true
            var handler = await GetIncidentHandler(handlerId);
            if (handler == null)
                return false;
            handler.IsDeleted = true;
            handler.UpdatedAt = DateTime.UtcNow;
            try
            {
                var response = await _container.UpsertItemAsync(handler, new PartitionKey(handler.PartitionKey ?? handler.Id));
                return response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Created;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<List<IncidentHandlerDocument>> QueryIncidentHandlers(List<string> keywords)
        {
            // Lowercase keywords for case-insensitive search
            var loweredKeywords = keywords.Select(k => k.ToLower()).ToArray();

            var queryable = _container.GetItemLinqQueryable<IncidentHandlerDocument>(allowSynchronousQueryExecution: false)
                .Where(c => c.DocumentType == DocumentType && c.IsDeleted == false &&
                            loweredKeywords.Any(kw => c.Name.ToLower().Contains(kw)));

            var iterator = queryable.ToFeedIterator();
            var results = new List<IncidentHandlerDocument>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
    }
}
