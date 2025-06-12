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
        Task<IncidentQueryResult<T>> QueryIncidents(IncidentQueryRequest request);
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
                    DocumentType = "IcmIncident";
                    break;
                case IncidentManagementType.AzMonitor:
                default:
                    throw new NotImplementedException($"Incident management type '{_incidentManagementSettings.Type}' is not implemented.");
            }
        }

        public async Task<IncidentQueryResult<T>> QueryIncidents(IncidentQueryRequest request)
        {
            switch (_incidentManagementSettings.Type)
            {
                case IncidentManagementType.PagerDuty:
                case IncidentManagementType.Icm:
                    return await QueryIncidentsInternal(request);

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
                case IncidentManagementType.Icm:
                    return await GetIncidentDetailsInternal(incidentId);
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

        private async Task<IncidentQueryResult<T>> QueryIncidentsInternal(IncidentQueryRequest request)
        {
            // Validate pagination parameters
            if (request.PageNumber <= 0)
            {
                request.PageNumber = 1;
            }
            if (request.PageSize <= 0)
            {
                request.PageSize = 20;
            }

            var pagedResult = new IncidentQueryResult<T>();
            var filteredResults = new List<T>();
            int totalCount = 0;
            if (request.DurationInDays > 90)
            {
                request.DurationInDays = 90;
            }
            var since = DateTime.UtcNow.AddDays(-request.DurationInDays);

            int skip = (request.PageNumber - 1) * request.PageSize;
            int take = request.PageSize;

            if (request.Filter == null)
            {
                if (request.Keywords == null || request.Keywords.Length == 0)
                {
                    pagedResult.Items = new List<T>();
                    pagedResult.TotalCount = 0;
                    return pagedResult;
                }

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
                filteredResults = results
                    .Where(c => loweredKeywords.Any(kw => c.Title != null && c.Title.ToLower().Contains(kw))).ToList();
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
                            Name = filterDocument.Name,
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
                    filteredResults = results
                        .Where(c => c.Title != null && c.Title.ToLower().Contains(loweredTitleContains)).ToList();
                }
                else
                {
                    filteredResults = results;
                }
            }

            totalCount = filteredResults.Count;
            pagedResult.TotalCount = totalCount;
            pagedResult.Items = filteredResults.Skip(skip).Take(take).ToList();
            return pagedResult;
        }

        private async Task<T?> GetIncidentDetailsInternal(string incidentId)
        {
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
        }
    }
}

public class IncidentQueryRequest
{
    public IncidentFilterDocumentPayload Filter { get; set; } = null;

    // Should only use Keywords in special scenarios where Filter isn't viable
    public string[] Keywords { get; set; } = [];
    public int DurationInDays { get; set; } = 60; // Default to 60 days for incident history

    // Pagination
    public int PageNumber { get; set; } = 1; // 1-based index
    public int PageSize { get; set; } = 20;  // Default page size
}

public class IncidentQueryResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
}
