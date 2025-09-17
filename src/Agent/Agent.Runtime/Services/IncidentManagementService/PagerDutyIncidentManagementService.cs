// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Graph.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

public class PagerDutyIncidentManagementService : IncidentManagementServiceBase<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload>
{
    private readonly IPagerDutyService _pagerDutyService;
    public PagerDutyIncidentManagementService(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IIncidentFilterManagementService<PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload> incidentFilterManagementService,
        ILogger<PagerDutyIncidentManagementService> logger,
        IPagerDutyService pagerDutyService)
        : base(
            cosmosClient.GetContainer(
            cosmosDbSettings.Docs.Database,
            AgentDataConfiguration.ThreadContainerName),
            incidentFilterManagementService,
            logger)
    {
        _pagerDutyService = pagerDutyService;
    }

    protected override string DocumentType => "PagerDutyIncident";

    public async override Task<PagerDutyIncidentDocument?> GetIncidentDetails(string incidentId)
    {
        try
        {
            return await GetIncidentDetailsInternal(incidentId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while fetching PagerDuty incident details for IncidentId: {IncidentId}", incidentId);
            throw;

        }
    }

    public override async Task<IncidentQueryResult<PagerDutyIncidentDocument>> QueryIncidents(IncidentQueryRequest<PagerDutyIncidentFilterDocumentPayload> request)
    {
        //return await QueryIncidentsInternal(request);
        _logger.LogInternalInformation(
                "QueryIcmIncidentsInternal: Invoked with Request: {Request}",
                Newtonsoft.Json.JsonConvert.SerializeObject(request)
            );
        if (request is null)
        {
            return new IncidentQueryResult<PagerDutyIncidentDocument>
            {
                Items = [],
                TotalCount = 0
            };
        }
        try
        {
            PagerDutyIncidentFilterDocumentPayload? filter = null;

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

            List<PagerDutyIncident> incidents = new List<PagerDutyIncident>();
            if (filter is not null)
            {
                incidents = (await _pagerDutyService.GetIncidentsAsync(
                    limit: limit,
                    offset: offset,
                    since: since,
                    impactServiceId: filter.ImpactedService,
                    titleContains: filter.TitleContains,
                    priority: filter.Priority,
                    statuses: statusFilter
                )).ToList();
            }
            else
            {
                //No filter in db or request
                //Take first keyword if available, otherwise use empty string
                string keyword = request?.Keywords?.FirstOrDefault() ?? string.Empty;
                incidents = (await _pagerDutyService.GetIncidentsAsync(
                    limit: limit,
                    offset: offset,
                    since: since,
                    titleContains: keyword,
                    statuses: statusFilter
                )).ToList();
            }
            _logger.LogInternalInformation(
                "QueryIcmIncidentsInternal: Retrieved {Count} incidents from ICM API",
                incidents.Count
            );

            var docs = incidents.Select(incident =>
                new PagerDutyIncidentDocument(
                    Id: incident.IncidentId,
                    HtmlUrl: incident.HtmlUrl,
                    CreatedAt: incident.CreatedAt,
                    Status: incident.Status,
                    IncidentType: incident.IncidentType?.Name ?? string.Empty,
                    ImpactedServiceId: incident.ImpactedService?.Id ?? "Not set",
                    ImpactedServiceName: incident.ImpactedService?.Summary ?? "Not set",
                    Priority: incident.Priority?.Summary ?? "Not set",
                    Urgency: incident.Urgency ?? "Not set")
                {
                    Title = incident.Title,
                    Description = incident.FirstTriggerLogEntry.Channel?.Details ?? incident.Description,
                    UpdatedAt = DateTime.UtcNow
                }).ToList();

            return new IncidentQueryResult<PagerDutyIncidentDocument>
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
