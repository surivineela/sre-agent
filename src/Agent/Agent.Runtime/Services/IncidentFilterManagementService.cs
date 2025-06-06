using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace Agent.Runtime.Services
{
    public interface IIncidentFilterManagementService
    {
        Task<List<IncidentFilterDocument>> ListIncidentFilters();
        Task<IncidentFilterDocument?> GetIncidentFilter(string filterId);
        Task<IncidentFilterDocument> SaveIncidentFilter(IncidentFilterDocument IncidentFilterDocument);
        Task<bool> DeleteIncidentFilter(string filterId);
    }

    public class IncidentFilterManagementService : IIncidentFilterManagementService
    {
        private readonly Container _container;
        protected readonly string DocumentType = "IncidentFilter";

        public IncidentFilterManagementService(CosmosClient cosmosClient, CosmosDBSettings cosmosDbSettings)
        {
            _container = cosmosClient.GetContainer(
                cosmosDbSettings.Docs.Database,
                AgentDataConfiguration.ThreadContainerName
            );
        }

        public async Task<List<IncidentFilterDocument>> ListIncidentFilters()
        {
            // Return all incident filters
            var queryable = _container.GetItemLinqQueryable<IncidentFilterDocument>(allowSynchronousQueryExecution: false)
                .Where(c => c.DocumentType == DocumentType && c.IsDeleted == false);

            var iterator = queryable.ToFeedIterator();
            var results = new List<IncidentFilterDocument>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }
            return results;
        }

        public async Task<IncidentFilterDocument?> GetIncidentFilter(string filterId)
        {
            var queryable = _container.GetItemLinqQueryable<IncidentFilterDocument>(allowSynchronousQueryExecution: false)
                .Where(c => c.DocumentType == DocumentType && c.Id == filterId && c.IsDeleted == false)
                .Take(1);

            var iterator = queryable.ToFeedIterator();
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                return response.FirstOrDefault();
            }
            return null;
        }

        public async Task<IncidentFilterDocument> SaveIncidentFilter(IncidentFilterDocument document)
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

        public async Task<bool> DeleteIncidentFilter(string filterId)
        {
            // do a soft delete by setting isDeleted to true
            var filter = await GetIncidentFilter(filterId);
            if (filter == null)
                return false;
            filter.IsDeleted = true;
            filter.UpdatedAt = DateTime.UtcNow;
            try
            {
                var response = await _container.UpsertItemAsync(filter, new PartitionKey(filter.PartitionKey ?? filter.Id));
                return response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Created;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
