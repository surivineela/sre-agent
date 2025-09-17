// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Models.ICM;
using Agent.Core.Services;
using Agent.Data;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

public class IcmIncidentManagementService : IncidentManagementServiceBase<IcmIncidentDocument, IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload>
{
    protected override string DocumentType => "IcmIncident";
    private readonly IICMAPIClient _icmApiClient;
    public IcmIncidentManagementService(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IIncidentFilterManagementService<IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload> incidentFilterManagementService,
        ILogger<IcmIncidentManagementService> logger,
        IICMAPIClient icmApiClient)
        : base(
            cosmosClient.GetContainer(
            cosmosDbSettings.Docs.Database,
            AgentDataConfiguration.ThreadContainerName),
            incidentFilterManagementService,
            logger)
    {
        _icmApiClient = icmApiClient;
    }

    public override async Task<IcmIncidentDocument?> GetIncidentDetails(string incidentId)
    {
        _logger.LogInternalInformation("GetIncidentDetails: Invoked for IncidentId: {IncidentId}", incidentId);
        try
        {
            return await GetIncidentDetailsInternal(incidentId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error fetching incident details for {IncidentId}", incidentId);
            throw;
        }
    }

    public override async Task<IncidentQueryResult<IcmIncidentDocument>> QueryIncidents(IncidentQueryRequest<IcmIncidentFilterDocumentPayload>? request)
    {
        _logger.LogInternalInformation(
                "QueryIcmIncidentsInternal: Invoked with Request: {Request}",
                Newtonsoft.Json.JsonConvert.SerializeObject(request)
            );
        if (request is null)
        {
            return new IncidentQueryResult<IcmIncidentDocument>
            {
                Items = [],
                TotalCount = 0
            };
        }
        try
        {
            IcmIncidentFilterDocumentPayload? filter = null;

            var statusFilter = request.Statuses?.Select(s => s.ToLower()).ToList() ?? [];
            uint limit = request.PageSize > 0 ? (uint)request.PageSize : 20;
            uint offset = (uint)((request.PageNumber - 1) * limit);
            var status = request.Statuses ?? [];
            var since = request.DurationInDays < 90 ? DateTime.UtcNow.AddDays(-request.DurationInDays) : DateTime.UtcNow.AddDays(-90);


            if (!string.IsNullOrEmpty(request?.Filter?.Id))
            {
                _logger.LogInternalInformation(
                    "QueryIcmIncidentsInternal: Fetching filter document for FilterId: {FilterId}",
                    request.Filter.Id
                );
                var filterDocument = await _incidentFilterManagementService.GetIncidentFilter(request.Filter.Id);
                if (filterDocument is not null)
                {
                    filter = filterDocument;
                }
                else
                {
                    _logger.LogInternalWarning(
                        "QueryIcmIncidentsInternal: No filter document found for FilterId: {FilterId}",
                        request.Filter.Id
                    );
                }
            }

            //If filter cannot find from db, try with request payload
            if (filter is null)
            {
                filter = request?.Filter;
            }

            List<Incident> incidents = new List<Incident>();
            if (filter is not null)
            {
                incidents = await _icmApiClient.GetIncidentsAsync(
                    limit: limit,
                    offset: offset,
                    lastModifiedDate: since,
                    owningServiceId: filter.ImpactedService,
                    titleContains: filter.TitleContains,
                    owningTeamId: filter.OwningTeamId,
                    incidentType: filter.IncidentType,
                    createdBy: filter.CreatedBy,
                    monitorId: filter.MonitorId,
                    severity: filter.Priority,
                    statuses: statusFilter
                );
            }
            else
            {
                //Take first keyword if available, otherwise use empty string
                string keyword = request?.Keywords?.FirstOrDefault() ?? string.Empty;
                incidents = await _icmApiClient.GetIncidentsAsync(
                    limit: limit,
                    offset: offset,
                    lastModifiedDate: since,
                    owningServiceId: request?.Filter?.ImpactedService,
                    titleContains: request?.Filter?.TitleContains ?? keyword,
                    owningTeamId: request?.Filter?.OwningTeamId,
                    incidentType: request?.Filter?.IncidentType,
                    createdBy: request?.Filter?.CreatedBy,
                    monitorId: request?.Filter?.MonitorId,
                    severity: request?.Filter?.Priority,
                    statuses: statusFilter
                );
            }
            _logger.LogInternalInformation(
                "QueryIcmIncidentsInternal: Retrieved {Count} incidents from ICM API",
                incidents.Count
            );

            var docs = incidents.Select(i => new IcmIncidentDocument(i)).ToList();

            return new IncidentQueryResult<IcmIncidentDocument>
            {
                Items = docs,
                TotalCount = docs.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "QueryIcmIncidentsInternal: Exception occurred for Request: {Request}",
                Newtonsoft.Json.JsonConvert.SerializeObject(request)
            );
            throw;
        }
    }
}
