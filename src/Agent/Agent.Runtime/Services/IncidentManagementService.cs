using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DataModels;
using Kusto.Cloud.Platform.Utils;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.Services
{
    public interface IIncidentManagementService<T> where T : IIncidentDocument
    {
        Task<List<T>> QueryIncidents(IncidentQueryRequest request);
        Task<T?> GetIncidentDetails(string incidentId);
        Task<T?> SaveDocument(T document);
    }

    public class IncidentManagementService<T> : IIncidentManagementService<T> where T : IIncidentDocument
    {
        private readonly Container _container;
        private readonly IncidentManagementSettings _incidentManagementSettings;
        protected readonly string DocumentType;
        private readonly IIncidentFilterManagementService _incidentFilterManagementService;

        public IncidentManagementService(CosmosClient cosmosClient, CosmosDBSettings cosmosDbSettings, IncidentManagementSettings incidentManagementSettings, IIncidentFilterManagementService incidentFilterManagementService)
        {
            _container = cosmosClient.GetContainer(
                cosmosDbSettings.Docs.Database,
                AgentDataConfiguration.ThreadContainerName
            );
            _incidentManagementSettings = incidentManagementSettings;
            _incidentFilterManagementService = incidentFilterManagementService;

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

        public async Task<List<T>> QueryIncidents(IncidentQueryRequest request)
        {
            switch (_incidentManagementSettings.Type)
            {
                case IncidentManagementType.PagerDuty:
                    return await QueryIncidentsInternal(request);
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

        private async Task<List<T>> QueryIncidentsInternal(IncidentQueryRequest request, int durationInDays = 60)
        {
            var since = DateTime.UtcNow.AddDays(-durationInDays);
            if (request.Filter == null)
            {
                if (request.Keywords == null || request.Keywords.Length == 0)
                    return new List<T>();

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
                var loweredKeywords = request.Keywords.Select(k => k.ToLower()).ToArray();
                return results
                    .Where(c => loweredKeywords.Any(kw => c.Title != null && c.Title.ToLower().Contains(kw)))
                    .ToList();
            }
            else
            {
                // Use the filter to query incidents
                var queryable = _container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: false)
                    .Where(c => c.DocumentType == DocumentType && c.CreatedAt >= since);

                IncidentFilterDocumentPayload filter = null;

                if (request.Filter.Id != null && request.Filter.Id.IsNotEmpty())
                {
                    var filterDocument = await _incidentFilterManagementService.GetIncidentFilter(request.Filter.Id);
                    if (filterDocument != null)
                    {
                        filter = new IncidentFilterDocumentPayload()
                        {
                            Id = filterDocument.Id,
                            ImpactedService = filterDocument.ImpactedService,
                            IncidentType = filterDocument.IncidentType,
                            Priority = filterDocument.Priority,
                            TitleContains = filterDocument.TitleContains,
                            AlertId = filterDocument.AlertId
                        };
                    }
                }

                // If filter hasn't been found or filterId is empty, then use the filter attributes from incoming request
                if (filter == null)
                {
                    filter = request.Filter;
                }

                if (filter.ImpactedService.IsNotEmpty())
                {
                    queryable = queryable.Where(c => c.ImpactedServiceId == filter.ImpactedService);
                }
                if (filter.Priority.IsNotEmpty())
                {
                    queryable = queryable.Where(c => c.Priority == filter.Priority);
                }
                if (filter.IncidentType.IsNotEmpty())
                {
                    queryable = queryable.Where(c => c.IncidentType == filter.IncidentType);
                }
                var iterator = queryable.ToFeedIterator();
                var results = new List<T>();
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                // Filter by TitleContains if provided
                if (filter.TitleContains != null && filter.TitleContains.Length > 0)
                {
                    var loweredTitleContains = filter.TitleContains.ToLower();
                    return results
                        .Where(c => c.Title != null && c.Title.ToLower().Contains(loweredTitleContains))
                        .ToList();
                }
                return results;
            }

        }
    }
}

public class IncidentQueryRequest
{
    public IncidentFilterDocumentPayload Filter { get; set; } = null;

    // Should only use Keywords in special scenarios where Filter isn't viable
    public string[] Keywords { get; set; } = [];
}
