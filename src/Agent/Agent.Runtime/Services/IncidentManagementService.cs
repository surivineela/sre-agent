using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.ServiceNow;
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
        private readonly IServiceNowAPIClient _serviceNowAPIClient;

        public IncidentManagementService(CosmosClient cosmosClient,
            CosmosDBSettings cosmosDbSettings,
            IncidentManagementSettings incidentManagementSettings,
            IIncidentFilterManagementService incidentFilterManagementService,
            ILogger<IncidentManagementService<T>> logger,
            IServiceNowAPIClient serviceNowAPIClient)
        {
            _container = cosmosClient.GetContainer(
                cosmosDbSettings.Docs.Database,
                AgentDataConfiguration.ThreadContainerName
            );
            _incidentManagementSettings = incidentManagementSettings;
            _incidentFilterManagementService = incidentFilterManagementService;
            _serviceNowAPIClient = serviceNowAPIClient;

            switch (_incidentManagementSettings.Type)
            {
                case IncidentManagementType.PagerDuty:
                    DocumentType = "PagerDutyIncident";
                    break;
                case IncidentManagementType.Icm:
                    DocumentType = "IcmIncident";
                    break;
                case IncidentManagementType.AzMonitor:
                    DocumentType = "AzMonitorAlert";
                    break;

                case IncidentManagementType.ServiceNow:
                    DocumentType = "ServiceNowIncident";
                    break;
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
                    case IncidentManagementType.ServiceNow:
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
                    //case IncidentManagementType.ServiceNow:
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
                    case IncidentManagementType.ServiceNow:
                        // For ServiceNow, incidentId is the incident number
                        // We need to get the sys_id first
                        var sysId = await GetServiceNowSysId(incidentId);
                        if (string.IsNullOrEmpty(sysId))
                        {
                            _logger.LogInternalWarning(
                                "GetIncidentDetails: Could not find sys_id for ServiceNow incident number: {IncidentNumber}",
                                incidentId
                            );
                            return default;
                        }

                        _logger.LogInternalInformation(
                            "GetIncidentDetails: Found sys_id {SysId} for ServiceNow incident number: {IncidentNumber}",
                            sysId, incidentId
                        );

                        // Now get the incident details using the sys_id
                        var serviceNowResult = await GetIncidentDetailsInternal(incidentId);
                        if (serviceNowResult == null)
                        {
                            _logger.LogInternalWarning(
                                "GetIncidentDetails: No incident found for ServiceNow incident number: {IncidentNumber}",
                                incidentId
                            );
                        }
                        else
                        {
                            _logger.LogInternalInformation(
                                "GetIncidentDetails: Successfully retrieved incident for ServiceNow incident number: {IncidentNumber}",
                                incidentId
                            );
                        }
                        return serviceNowResult;
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

        private async Task<string> GetServiceNowSysId(string incidentNumber)
        {
            _logger.LogInternalInformation(
                "GetServiceNowSysId: Retrieving sys_id for incident number: {IncidentNumber}",
                incidentNumber
            );

            try
            {
                if (_serviceNowAPIClient == null)
                {
                    _logger.LogInternalError(
                        "GetServiceNowSysId: ServiceNowAPIClient is not initialized"
                    );
                    return string.Empty;
                }

                if (string.IsNullOrEmpty(incidentNumber))
                {
                    _logger.LogInternalError(
                        "GetServiceNowSysId: Incident number is null or empty"
                    );
                    return string.Empty;
                }

                // Add debug logging to see what's being queried and returned
                _logger.LogInternalInformation(
                    "GetServiceNowSysId: Querying for documents with DocumentType={DocumentType} and Number={Number}",
                    "ServiceNowIncident", incidentNumber
                );

                // First, try to get the document from the database as it already contains the sys_id
                var query = _container.GetItemLinqQueryable<ServiceNowIncidentDocument>(allowSynchronousQueryExecution: false)
                    .Where(c => c.DocumentType == "ServiceNowIncident" && c.Number == incidentNumber)
                    .Take(1);

                _logger.LogInternalInformation(
                    "GetServiceNowSysId: Query expression: {Query}", 
                    query.Expression.ToString()
                );

                var iterator = query.ToFeedIterator();

                if (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    var document = response.FirstOrDefault();
                    
                    if (document != null)
                    {
                        _logger.LogInternalInformation(
                            "GetServiceNowSysId: Found document: Id={Id}, Number={Number}, IncidentSystemId={IncidentSystemId}",
                            document.Id, document.Number, document.IncidentSystemId
                        );
                        
                        if (!string.IsNullOrEmpty(document.IncidentSystemId))
                        {
                            _logger.LogInternalInformation(
                                "GetServiceNowSysId: Found sys_id in document: {SysId} for incident number: {IncidentNumber}",
                                document.IncidentSystemId, incidentNumber
                            );
                            return document.IncidentSystemId;
                        }
                    }
                    else
                    {
                        _logger.LogInternalWarning(
                            "GetServiceNowSysId: No document found with Number={Number}",
                            incidentNumber
                        );
                    }
                }
                else
                {
                    _logger.LogInternalWarning(
                        "GetServiceNowSysId: No results from query for Number={Number}",
                        incidentNumber
                    );
                }

                // If we couldn't find the document in the database, query ServiceNow API
                var incidents = await _serviceNowAPIClient.GetIncidentsAsync(1, 0, null, null, null);
                var incident = incidents.FirstOrDefault(i => i.Number == incidentNumber);

                if (incident != null)
                {
                    _logger.LogInternalInformation(
                        "GetServiceNowSysId: Found sys_id via API: {SysId} for incident number: {IncidentNumber}",
                        incident.IncidentId, incidentNumber
                    );
                    return incident.IncidentId;
                }

                _logger.LogInternalWarning(
                    "GetServiceNowSysId: Could not find sys_id for incident number: {IncidentNumber}",
                    incidentNumber
                );
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(
                    ex,
                    "GetServiceNowSysId: Exception occurred for incident number: {IncidentNumber}",
                    incidentNumber
                );
                return string.Empty;
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

                    if (request.Statuses != null && request.Statuses.Count() > 0)
                    {
                        request.Statuses = request.Statuses.Select(s => s.ToLower()).ToArray();
                        queryable = queryable.Where(c => request.Statuses.Contains(c.Status.ToLower()));
                    }

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

                    if (request.Statuses != null && request.Statuses.Count() > 0) {
                        var normalizedStatuses = NormalizeStatusesForFiltering(request.Statuses.ToArray());
                        queryable = queryable.Where(c => normalizedStatuses.Contains(c.Status.ToLower()));
                    }

                    IncidentFilterDocumentPayload? filter = null;

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
                        queryable = queryable.Where(c => c.ImpactedServiceName.Equals(filter.ImpactedService, StringComparison.OrdinalIgnoreCase) || c.ImpactedServiceId.Equals(filter.ImpactedService, StringComparison.OrdinalIgnoreCase));
                    }
                    if (!string.IsNullOrEmpty(filter.Priority))
                    {
                        var normalizedPriorities = NormalizePriorityForFiltering(filter.Priority);
                        queryable = queryable.Where(c => normalizedPriorities.Contains(c.Priority.ToLower()));
                    }
                    if (!string.IsNullOrEmpty(filter.IncidentType))
                    {
                        queryable = queryable.Where(c => c.IncidentType.Equals(filter.IncidentType, StringComparison.OrdinalIgnoreCase));
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

        /// <summary>
        /// Normalizes status values for filtering, handling ServiceNow numeric states
        /// and converting human-readable status names to their corresponding values.
        /// </summary>
        /// <param name="statuses">Array of status values to normalize</param>
        /// <returns>Array of normalized status values for filtering</returns>
        private string[] NormalizeStatusesForFiltering(string[] statuses)
        {
            var normalizedStatuses = new List<string>();

            foreach (var status in statuses)
            {
                var lowerStatus = status.ToLower();
                normalizedStatuses.Add(lowerStatus);

                // For ServiceNow, also add numeric equivalents of common status names
                if (_incidentManagementSettings.Type == IncidentManagementType.ServiceNow)
                {
                    switch (lowerStatus)
                    {
                        case "new":
                            normalizedStatuses.Add("1");
                            break;
                        case "active":
                        case "in progress":
                        case "work in progress":
                            normalizedStatuses.Add("2");
                            break;
                        case "awaiting problem":
                            normalizedStatuses.Add("3");
                            break;
                        case "awaiting user info":
                            normalizedStatuses.Add("4");
                            break;
                        case "awaiting evidence":
                            normalizedStatuses.Add("5");
                            break;
                        case "resolved":
                            normalizedStatuses.Add("6");
                            break;
                        case "closed":
                            normalizedStatuses.Add("7");
                            break;
                        case "cancelled":
                        case "canceled":
                            normalizedStatuses.Add("8");
                            break;
                        default:
                            // If it's already a numeric value, also add common names
                            switch (lowerStatus)
                            {
                                case "1":
                                    normalizedStatuses.Add("new");
                                    break;
                                case "2":
                                    normalizedStatuses.Add("active");
                                    normalizedStatuses.Add("in progress");
                                    normalizedStatuses.Add("work in progress");
                                    break;
                                case "3":
                                    normalizedStatuses.Add("awaiting problem");
                                    break;
                                case "4":
                                    normalizedStatuses.Add("awaiting user info");
                                    break;
                                case "5":
                                    normalizedStatuses.Add("awaiting evidence");
                                    break;
                                case "6":
                                    normalizedStatuses.Add("resolved");
                                    break;
                                case "7":
                                    normalizedStatuses.Add("closed");
                                    break;
                                case "8":
                                    normalizedStatuses.Add("cancelled");
                                    normalizedStatuses.Add("canceled");
                                    break;
                            }
                            break;
                    }
                }
            }

            return normalizedStatuses.Distinct().ToArray();
        }

        private string[] NormalizePriorityForFiltering(string priority)
        {
            var normalizedPriorities = new List<string>();
            var lowerPriority = priority.ToLower();
            normalizedPriorities.Add(lowerPriority);

            // For ServiceNow, also add numeric equivalents of common priority names
            if (_incidentManagementSettings.Type == IncidentManagementType.ServiceNow)
            {
                switch (lowerPriority)
                {
                    case "critical":
                    case "1 - critical":
                        normalizedPriorities.Add("1");
                        break;
                    case "high":
                    case "2 - high":
                        normalizedPriorities.Add("2");
                        break;
                    case "moderate":
                    case "medium":
                    case "3 - moderate":
                        normalizedPriorities.Add("3");
                        break;
                    case "low":
                    case "4 - low":
                        normalizedPriorities.Add("4");
                        break;
                    case "planning":
                    case "5 - planning":
                        normalizedPriorities.Add("5");
                        break;
                    default:
                        // If it's already a numeric value, also add common names
                        switch (lowerPriority)
                        {
                            case "1":
                                normalizedPriorities.Add("critical");
                                break;
                            case "2":
                                normalizedPriorities.Add("high");
                                break;
                            case "3":
                                normalizedPriorities.Add("moderate");
                                normalizedPriorities.Add("medium");
                                break;
                            case "4":
                                normalizedPriorities.Add("low");
                                break;
                            case "5":
                                normalizedPriorities.Add("planning");
                                break;
                        }
                        break;
                }
            }

            return normalizedPriorities.Distinct().ToArray();
        }
    }
}

public class IncidentQueryRequest
{
    public IncidentFilterDocumentPayload? Filter { get; set; }

    // Should only use Keywords in special scenarios where Filter isn't viable
    public string[] Keywords { get; set; } = [];
    public int DurationInDays { get; set; } = 60; // Default to 60 days for incident history

    public string[] Statuses { get; set; } = [];

    // Pagination
    public int PageNumber { get; set; } = 1; // 1-based index
    public int PageSize { get; set; } = 20;  // Default page size
}

public class IncidentQueryResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
}
