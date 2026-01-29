// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.ServiceNow;
using Agent.Data;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Agent.Runtime.Services;

public class ServiceNowIncidentManagementService : IncidentManagementServiceBase<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocument, ServiceNowIncidentFilterDocumentPayload>
{
    protected override string DocumentType => "ServiceNowIncident";
    private readonly IServiceNowAPIClient _serviceNowAPIClient;

    public ServiceNowIncidentManagementService(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        ILogger<ServiceNowIncidentManagementService> logger,
        IIncidentFilterManagementService<ServiceNowIncidentFilterDocument, ServiceNowIncidentFilterDocumentPayload> incidentFilterManagementService,
        IServiceNowAPIClient serviceNowAPIClient)
        : base(
          cosmosClient.GetContainer(
            cosmosDbSettings.Docs.Database,
            AgentDataConfiguration.ThreadContainerName),
          incidentFilterManagementService,
          logger)
    {
        _serviceNowAPIClient = serviceNowAPIClient;
    }

    public async override Task<ServiceNowIncidentDocument?> GetIncidentAsync(string incidentId, bool fetchFromAPI = true)
    {
        _logger.LogInternalInformation("[ServiceNowIncidentManagementService] GetIncidentAsync: Invoked for IncidentId: {IncidentId}, FetchFromAPI: {FetchFromAPI}", incidentId, fetchFromAPI);
        try
        {
            // For ServiceNow, incidentId is the incident number
            // We need to get the sys_id first
            var sysId = await GetServiceNowSysId(incidentId);
            if (string.IsNullOrEmpty(sysId))
            {
                _logger.LogInternalWarning("[ServiceNowIncidentManagementService] GetIncidentAsync: Unable to find sys_id for ServiceNow incident number: {IncidentNumber}", incidentId);
                return default;
            }

            _logger.LogInternalInformation("[ServiceNowIncidentManagementService] GetIncidentAsync: Found sys_id {SysId} for ServiceNow incident number: {IncidentNumber}", sysId, incidentId);

            // Now get the incident details using the sys_id
            var serviceNowResult = await GetIncidentFromDBAsync(incidentId);

            if (serviceNowResult is null && fetchFromAPI == true)
            {
                _logger.LogInternalWarning("[ServiceNowIncidentManagementService] GetIncidentAsync: No incident data found for IncidentId: {IncidentId}, fetching latest", incidentId);
                var latestIncidentData = await _serviceNowAPIClient.GetIncidentAsync(incidentId);
                return new ServiceNowIncidentDocument(latestIncidentData);
            }
            _logger.LogInternalInformation(
                "[ServiceNowIncidentManagementService] GetIncidentAsync: Successfully retrieved incident for ServiceNow incident number: {IncidentNumber}",
                incidentId
            );
            return serviceNowResult;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[ServiceNowIncidentManagementService] GetIncidentAsync: Exception occurred for ServiceNow incident number: {IncidentNumber}", incidentId);
            throw;
        }
    }


    public override async Task<IncidentQueryResult<ServiceNowIncidentDocument>> QueryIncidents(IncidentQueryRequest<ServiceNowIncidentFilterDocumentPayload> request)
    {
        _logger.LogInternalInformation(
            "QueryIncidents: Invoked with Request: {Request}",
            JsonConvert.SerializeObject(request)
        );

        if (request is null)
        {
            return new IncidentQueryResult<ServiceNowIncidentDocument>
            {
                Items = [],
                TotalCount = 0
            };
        }

        try
        {
            ServiceNowIncidentFilterDocumentPayload? filter = null;
            var statusFilter = request.Statuses?.Select(s => s.ToLower()).ToList() ?? [];
            uint limit = request.PageSize > 0 ? (uint)request.PageSize : 20;
            uint offset = request.PageNumber > 0 ? (uint)((request.PageNumber - 1) * limit) : 0;

            // Calculate since date based on duration
            DateTime? since = request.DurationInDays > 0
                ? DateTime.UtcNow.AddDays(-request.DurationInDays)
                : null;

            // Try to get filter from database if FilterId is provided
            if (request.Filter?.Id != null)
            {
                try
                {
                    _logger.LogInternalInformation(
                        "QueryIncidents: Fetching filter document for FilterId: {FilterId}",
                        request.Filter.Id
                    );

                    var filterDocument = await _incidentFilterManagementService.GetIncidentFilter(request.Filter.Id);
                    if (filterDocument != null)
                    {
                        _logger.LogInternalInformation(
                            "QueryIncidents: Loaded filter document for FilterId: {FilterId}",
                            request.Filter.Id
                        );
                        filter = filterDocument;
                    }
                    else
                    {
                        _logger.LogInternalWarning(
                            "QueryIncidents: No filter document found for FilterId: {FilterId}",
                            request.Filter.Id
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex,
                        "QueryIncidents: Error fetching filter document for FilterId: {FilterId}",
                        request.Filter.Id
                    );
                }
            }

            // If filter cannot be found from db, try with request payload
            if (filter is null)
            {
                filter = request?.Filter;
            }

            List<ServiceNowIncident> incidents = new List<ServiceNowIncident>();

            if (filter is not null)
            {
                incidents = await _serviceNowAPIClient.GetIncidentsAsync(
                    limit: limit,
                    offset: offset,
                    lastModifiedDate: since,
                    serviceId: filter.ImpactedService,
                    titleContains: filter.TitleContains,
                    priorities: filter.Priorities
                );
            }
            else
            {
                // Take first keyword if available, otherwise use empty string
                string keyword = request?.Keywords?.FirstOrDefault() ?? string.Empty;
                incidents = await _serviceNowAPIClient.GetIncidentsAsync(
                    limit: limit,
                    offset: offset,
                    lastModifiedDate: since,
                    serviceId: null,
                    titleContains: !string.IsNullOrEmpty(keyword) ? keyword : null,
                    priorities: null
                );
            }

            _logger.LogInternalInformation(
                "QueryIncidents: Retrieved {Count} incidents from ServiceNow API",
                incidents.Count
            );

            // Convert to documents
            var docs = incidents.Select(i => new ServiceNowIncidentDocument(i)).ToList();

            // Apply status filtering if specified
            if (statusFilter.Any())
            {
                var normalizedStatuses = statusFilter.SelectMany(s => NormalizeStatusesForFiltering([s])).ToList();
                docs = docs.Where(d => normalizedStatuses.Contains(d.Status?.ToLower() ?? "")).ToList();

                _logger.LogInternalInformation(
                    "QueryIncidents: Filtered by status. Count after filtering: {Count}",
                    docs.Count
                );
            }


            return new IncidentQueryResult<ServiceNowIncidentDocument>
            {
                Items = docs,
                TotalCount = docs.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "QueryIncidents: Exception occurred for Request: {Request}",
                JsonConvert.SerializeObject(request)
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
            var incidents = await _serviceNowAPIClient.GetIncidentsAsync(1, 0, null, null, null, null);
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



    protected override string[] NormalizeStatusesForFiltering(IEnumerable<string> statuses)
    {
        return ServiceNowStatusHelper.NormalizeStatusesForFiltering(statuses);
    }

    public override string[] NormalizePriorityForFiltering(IEnumerable<string> priorities)
    {
        return ServiceNowPriorityHelper.NormalizePriorityForFiltering(priorities);
    }
}
