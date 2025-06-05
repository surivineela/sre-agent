using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.Services
{
    public interface IIncidentManagementService<T> where T : IIncidentDocument
    {
        Task<List<T>> QueryIncidents(string[] keywords);
        Task<T?> GetIncidentDetails(string incidentId);
        Task<T?> SaveDocument(T document);
    }

    public class IncidentManagementService<T> : IIncidentManagementService<T> where T : IIncidentDocument
    {
        private readonly Container _container;
        private readonly IncidentManagementSettings _incidentManagementSettings;
        protected readonly string DocumentType;

        public IncidentManagementService(CosmosClient cosmosClient, CosmosDBSettings cosmosDbSettings, IncidentManagementSettings incidentManagementSettings)
        {
            _container = cosmosClient.GetContainer(
                cosmosDbSettings.Docs.Database,
                AgentDataConfiguration.ThreadContainerName
            );
            _incidentManagementSettings = incidentManagementSettings;

            switch (_incidentManagementSettings.Type)
            {
                case IncidentManagementType.PagerDuty:
                    DocumentType = "PagerDutyIncident";
                    break;
                case IncidentManagementType.Icm:
                case IncidentManagementType.AzMonitor:
                default:
                    throw new NotImplementedException($"Incident management type '{_incidentManagementSettings.Type}' is not implemented.");
            }
        }

        public async Task<List<T>> QueryIncidents(string[] keywords)
        {
            switch (_incidentManagementSettings.Type)
            {
                case IncidentManagementType.PagerDuty:
                    return await QueryIncidentsInternal(keywords);
                case IncidentManagementType.Icm:
                case IncidentManagementType.AzMonitor:
                default:
                    throw new NotImplementedException($"Incident management type '{_incidentManagementSettings.Type}' is not implemented.");
            }
        }

        public async Task<T?> GetIncidentDetails(string incidentId)
        {
            switch (_incidentManagementSettings.Type)
            {
                case IncidentManagementType.PagerDuty:
                    var iterator = _container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: false)
                        .Where(c => c.DocumentType == DocumentType && c.Id == incidentId)
                        .Take(1)
                        .ToFeedIterator();

                    if (iterator.HasMoreResults)
                    {
                        var response = await iterator.ReadNextAsync();
                        return response.FirstOrDefault();
                    }
                    return default;
                default:
                    throw new NotImplementedException($"Incident management type '{_incidentManagementSettings.Type}' is not implemented.");
            }
        }

        public async Task<T?> SaveDocument(T document)
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

        private async Task<List<T>> QueryIncidentsInternal(string[] keywords, int durationInDays = 60)
        {
            if (keywords == null || keywords.Length == 0)
                return new List<T>();

            var since = DateTime.UtcNow.AddDays(-durationInDays);

            // Fetch only recent incidents from Cosmos DB
            var queryable = _container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: false)
                .Where(c => c.DocumentType == DocumentType && c.CreatedAt >= since);

            var iterator = queryable.ToFeedIterator();
            var results = new List<T>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            // Filter in-memory by keywords (case-insensitive)
            var loweredKeywords = keywords.Select(k => k.ToLower()).ToArray();
            return results
                .Where(c => loweredKeywords.Any(kw => c.Title != null && c.Title.ToLower().Contains(kw)))
                .ToList();
        }
    }
}
