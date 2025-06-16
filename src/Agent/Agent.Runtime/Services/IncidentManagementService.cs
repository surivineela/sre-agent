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
using Microsoft.Extensions.Logging;
using Agent.Logging;

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
        private readonly ILogger<IncidentManagementService<T>> _logger;
        private readonly IIncidentFilterManagementService _incidentFilterManagementService;

        public IncidentManagementService(CosmosClient cosmosClient,
            CosmosDBSettings cosmosDbSettings,
            IncidentManagementSettings incidentManagementSettings,
            IIncidentFilterManagementService incidentFilterManagementService,
            ILogger<IncidentManagementService<T>> logger)
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

            _logger = logger;
        }

        public async Task<IncidentQueryResult<T>> QueryIncidents(IncidentQueryRequest request)
        {
            _logger.LogInternalInformation(
                "QueryIncidents: Invoked with Request: {Request}",
                Newtonsoft.Json.JsonConvert.SerializeObject(request)
            );

            try
            {
                switch (_incidentManagementSettings.Type)
                {
                    case IncidentManagementType.PagerDuty:
                    case IncidentManagementType.Icm:
                        var result = await QueryIncidentsInternal(request);
                        _logger.LogInternalInformation(
                            "QueryIncidents: Successfully queried incidents. TotalCount: {TotalCount}",
                            result.TotalCount
                        );
                        return result;

                    case IncidentManagementType.AzMonitor:
                    default:
                        _logger.LogInternalWarning(
                            "QueryIncidents: Not implemented for IncidentManagementType: {Type}",
                            _incidentManagementSettings.Type
                        );
                        throw new NotImplementedException($"Incident management type '{_incidentManagementSettings.Type}' is not implemented.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(
                    ex,
                    "QueryIncidents: Exception occurred for Request: {Request}",
                    Newtonsoft.Json.JsonConvert.SerializeObject(request)
                );
                throw;
            }
        }

        public async Task<T?> GetIncidentDetails(string incidentId)
        {
            _logger.LogInternalInformation(
                "GetIncidentDetails: Invoked for IncidentId: {IncidentId}",
                incidentId
            );

            try
            {
                switch (_incidentManagementSettings.Type)
                {
                    case IncidentManagementType.PagerDuty:
                    case IncidentManagementType.Icm:
                        var result = await GetIncidentDetailsInternal(incidentId);
                        if (result == null)
                        {
                            _logger.LogInternalWarning(
                                "GetIncidentDetails: No incident found for IncidentId: {IncidentId}",
                                incidentId
                            );
                        }
                        else
                        {
                            _logger.LogInternalInformation(
                                "GetIncidentDetails: Successfully retrieved incident for IncidentId: {IncidentId}",
                                incidentId
                            );
                        }
                        return result;
                    default:
                        _logger.LogInternalWarning(
                            "GetIncidentDetails: Not implemented for IncidentManagementType: {Type}",
                            _incidentManagementSettings.Type
                        );
                        throw new NotImplementedException($"Incident management type '{_incidentManagementSettings.Type}' is not implemented.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(
                    ex,
                    "GetIncidentDetails: Exception occurred for IncidentId: {IncidentId}",
                    incidentId
                );
                throw;
            }
        }

        public async Task<T?> SaveDocument(T document)
        {
            _logger.LogInternalInformation(
                "SaveDocument: Invoked for DocumentId: {DocumentId}, DocumentType: {DocumentType}",
                document?.Id,
                document?.DocumentType
            );

            try
            {
                if (document == null)
                {
                    _logger.LogInternalWarning(
                        "SaveDocument: Document is null"
                    );
                    throw new ArgumentNullException(nameof(document));
                }
                var response = await _container.UpsertItemAsync(document, new PartitionKey(document.PartitionKey ?? document.Id));
                _logger.LogInternalInformation(
                    "SaveDocument: Successfully saved DocumentId: {DocumentId}, DocumentType: {DocumentType}",
                    document.Id,
                    document.DocumentType
                );
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(
                    ex,
                    "SaveDocument: Exception occurred for DocumentId: {DocumentId}, DocumentType: {DocumentType}",
                    document?.Id,
                    document?.DocumentType
                );
                throw;
            }
        }

        private async Task<IncidentQueryResult<T>> QueryIncidentsInternal(IncidentQueryRequest request)
        {
            _logger.LogInternalInformation(
                "QueryIncidentsInternal: Invoked with Request: {Request}",
                Newtonsoft.Json.JsonConvert.SerializeObject(request)
            );

            try
            {
                // Validate pagination parameters
                if (request.PageNumber <= 0)
                {
                    _logger.LogInternalWarning(
                        "QueryIncidentsInternal: PageNumber <= 0, resetting to 1"
                    );
                    request.PageNumber = 1;
                }
                if (request.PageSize <= 0)
                {
                    _logger.LogInternalWarning(
                        "QueryIncidentsInternal: PageSize <= 0, resetting to 20"
                    );
                    request.PageSize = 20;
                }

                var pagedResult = new IncidentQueryResult<T>();
                var filteredResults = new List<T>();
                int totalCount = 0;
                if (request.DurationInDays > 90)
                {
                    _logger.LogInternalWarning(
                        "QueryIncidentsInternal: DurationInDays > 90, resetting to 90"
                    );
                    request.DurationInDays = 90;
                }
                var since = DateTime.UtcNow.AddDays(-request.DurationInDays);

                int skip = (request.PageNumber - 1) * request.PageSize;
                int take = request.PageSize;

                if (request.Filter == null)
                {
                    if (request.Keywords == null || request.Keywords.Length == 0)
                    {
                        _logger.LogInternalWarning(
                            "QueryIncidentsInternal: No filter and no keywords provided"
                        );
                        pagedResult.Items = new List<T>();
                        pagedResult.TotalCount = 0;
                        return pagedResult;
                    }

                    // Fetch only recent incidents from Cosmos DB
                    _logger.LogInternalInformation(
                        "QueryIncidentsInternal: Querying Cosmos DB for DocumentType: {DocumentType} since {Since}",
                        DocumentType, since
                    );
                    var queryable = _container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: false)
                        .Where(c => c.DocumentType == DocumentType && c.CreatedAt >= since);

                    var iterator = queryable.ToFeedIterator();
                    var results = new List<T>();

                    while (iterator.HasMoreResults)
                    {
                        var response = await iterator.ReadNextAsync();
                        results.AddRange(response);
                    }

                    _logger.LogInternalInformation(
                        "QueryIncidentsInternal: Retrieved {Count} incidents from Cosmos DB",
                        results.Count
                    );

                    // Filter in-memory by keywords (case-insensitive)
                    var loweredKeywords = request.Keywords.Select(k => k.ToLower()).ToArray();
                    filteredResults = results
                        .Where(c => loweredKeywords.Any(kw => c.Title != null && c.Title.ToLower().Contains(kw))).ToList();

                    _logger.LogInternalInformation(
                        "QueryIncidentsInternal: Filtered results by keywords. FilteredCount: {FilteredCount}",
                        filteredResults.Count
                    );
                }
                else
                {
                    // Use the filter to query incidents
                    _logger.LogInternalInformation(
                        "QueryIncidentsInternal: Querying with filter: {Filter}",
                        Newtonsoft.Json.JsonConvert.SerializeObject(request.Filter)
                    );
                    var queryable = _container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: false)
                        .Where(c => c.DocumentType == DocumentType && c.CreatedAt >= since);

                    IncidentFilterDocumentPayload filter = null;

                    if (!string.IsNullOrEmpty(request.Filter.Id))
                    {
                        _logger.LogInternalInformation(
                            "QueryIncidentsInternal: Fetching filter document for FilterId: {FilterId}",
                            request.Filter.Id
                        );
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
                            _logger.LogInternalInformation(
                                "QueryIncidentsInternal: Loaded filter document for FilterId: {FilterId}",
                                filterDocument.Id
                            );
                        }
                        else
                        {
                            _logger.LogInternalWarning(
                                "QueryIncidentsInternal: No filter document found for FilterId: {FilterId}",
                                request.Filter.Id
                            );
                        }
                    }

                    // If filter hasn't been found or filterId is empty, then use the filter attributes from incoming request
                    if (filter == null)
                    {
                        filter = request.Filter;
                    }

                    if (!string.IsNullOrEmpty(filter.ImpactedService))
                    {
                        queryable = queryable.Where(c => c.ImpactedServiceName == filter.ImpactedService || c.ImpactedServiceId == filter.ImpactedService);
                    }
                    if (!string.IsNullOrEmpty(filter.Priority))
                    {
                        queryable = queryable.Where(c => c.Priority == filter.Priority);
                    }
                    if (!string.IsNullOrEmpty(filter.IncidentType))
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

                    _logger.LogInternalInformation(
                        "QueryIncidentsInternal: Retrieved {Count} incidents from Cosmos DB with filter",
                        results.Count
                    );

                    // Filter by TitleContains if provided
                    if (filter.TitleContains != null && filter.TitleContains.Length > 0)
                    {
                        var loweredTitleContains = filter.TitleContains.ToLower();
                        filteredResults = results
                            .Where(c => c.Title != null && c.Title.ToLower().Contains(loweredTitleContains)).ToList();

                        _logger.LogInternalInformation(
                            "QueryIncidentsInternal: Filtered results by TitleContains. FilteredCount: {FilteredCount}",
                            filteredResults.Count
                        );
                    }
                    else
                    {
                        filteredResults = results;
                    }
                }

                totalCount = filteredResults.Count;
                pagedResult.TotalCount = totalCount;
                pagedResult.Items = filteredResults.OrderByDescending(c => c.CreatedAt).Skip(skip).Take(take).ToList();

                _logger.LogInternalInformation(
                    "QueryIncidentsInternal: Returning paged result. PageNumber: {PageNumber}, PageSize: {PageSize}, TotalCount: {TotalCount}",
                    request.PageNumber, request.PageSize, totalCount
                );

                return pagedResult;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(
                    ex,
                    "QueryIncidentsInternal: Exception occurred for Request: {Request}",
                    Newtonsoft.Json.JsonConvert.SerializeObject(request)
                );
                throw;
            }
        }

        private async Task<T?> GetIncidentDetailsInternal(string incidentId)
        {
            _logger.LogInternalInformation(
                "GetIncidentDetailsInternal: Invoked for IncidentId: {IncidentId}",
                incidentId
            );

            try
            {
                var iterator = _container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: false)
                            .Where(c => c.DocumentType == DocumentType && c.Id == incidentId)
                            .Take(1)
                            .ToFeedIterator();

                if (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    var result = response.FirstOrDefault();
                    if (result == null)
                    {
                        _logger.LogInternalWarning(
                            "GetIncidentDetailsInternal: No incident found for IncidentId: {IncidentId}",
                            incidentId
                        );
                    }
                    else
                    {
                        _logger.LogInternalInformation(
                            "GetIncidentDetailsInternal: Successfully retrieved incident for IncidentId: {IncidentId}",
                            incidentId
                        );
                    }
                    return result;
                }
                _logger.LogInternalWarning(
                    "GetIncidentDetailsInternal: No results for IncidentId: {IncidentId}",
                    incidentId
                );
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(
                    ex,
                    "GetIncidentDetailsInternal: Exception occurred for IncidentId: {IncidentId}",
                    incidentId
                );
                throw;
            }
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
