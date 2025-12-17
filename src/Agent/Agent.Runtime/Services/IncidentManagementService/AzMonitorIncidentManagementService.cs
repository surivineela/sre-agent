// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Data.Interface.IncidentAPI;
using Azure.Core;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

public class AzMonitorIncidentManagementService : IncidentManagementServiceBase<AzMonitorAlertDocument, AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload>
{
    protected override string DocumentType => $"{nameof(IncidentManagementType.AzMonitor)}Incident";
    private IAzMonitorAlertService _azMonitorAlertService;

    public AzMonitorIncidentManagementService(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IIncidentFilterManagementService<AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload> incidentFilterManagementService,
        ILogger<AzMonitorIncidentManagementService> logger,
        IAzMonitorAlertService azMonitorAlertService)
        : base(
            cosmosClient.GetContainer(
            cosmosDbSettings.Docs.Database,
            AgentDataConfiguration.ThreadContainerName),
            incidentFilterManagementService,
            logger)
    {
        _azMonitorAlertService = azMonitorAlertService;
    }

    public override async Task<AzMonitorAlertDocument?> GetIncidentAsync(string incidentId, bool fetchFromAPI = true)
    {
        _logger.LogInternalInformation("[AzMonitorIncidentManagementService] GetIncidentAsync: Invoked for IncidentId: {IncidentId}, FetchFromAPI: {FetchFromAPI}", incidentId, fetchFromAPI);
        try
        {
            var incidentGUID = new ResourceIdentifier(incidentId).Name ?? incidentId;
            var incidentDoc = await GetIncidentFromDBAsync(incidentId);
            if (incidentDoc is null && fetchFromAPI == true)
            {
                _logger.LogInternalWarning("[AzMonitorIncidentManagementService] GetIncidentAsync: No incident data found for IncidentId: {IncidentId}, fetching latest", incidentId);
                var lastestIncident = await _azMonitorAlertService.GetIncidentAsync(incidentId);
                return AzMonitorAlertDocument.FromIncident(lastestIncident);
            }
            _logger.LogInternalInformation("[AzMonitorIncidentManagementService] GetIncidentAsync: Returning incident data for IncidentId: {IncidentId}", incidentId);
            return incidentDoc;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[AzMonitorIncidentManagementService] GetIncidentAsync: Error occurred for IncidentId: {IncidentId}", incidentId);
            throw;
        }
    }

    public override async Task<IncidentQueryResult<AzMonitorAlertDocument>> QueryIncidents(IncidentQueryRequest<AzMonitorIncidentFilterDocumentPayload> request)
    {
        AzMonitorIncidentFilterDocumentPayload? filter = null;

        var statusFilter = request.Statuses?.Select(s => s.ToLower()).ToList() ?? [];
        uint limit = request.PageSize > 0 ? (uint)request.PageSize : 20;
        uint offset = (uint)((request.PageNumber - 1) * limit);

        var statuses = MapToAzMonitorStatuses(request.Statuses);
        var since = request.DurationInDays < 90 ? DateTime.UtcNow.AddDays(-request.DurationInDays) : DateTime.UtcNow.AddDays(-90);

        if (!string.IsNullOrEmpty(request?.Filter?.Id))
        {
            _logger.LogInternalInformation(
                "QueryAzMonitorIncidentsInternal: Fetching filter document for FilterId: {FilterId}",
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
                    "QueryAzMonitorIncidentsInternal: No filter document found for FilterId: {FilterId}",
                    request.Filter.Id
                );
            }
        }

        //If filter cannot find from db, try with request payload
        if (filter is null)
        {
            filter = request?.Filter;
        }

        if (filter is null)
        {
            filter = new AzMonitorIncidentFilterDocumentPayload()
            {
                TitleContains = request?.Keywords.FirstOrDefault() ?? string.Empty
            };
        }
        var incidents = await _azMonitorAlertService.GetIncidentsAsync(
                limit: limit,
                offset: offset,
                since: since,
                statuses: statuses,
                filterPayload: filter
                );
        return new IncidentQueryResult<AzMonitorAlertDocument>
        {
            Items = [.. incidents.Select(AzMonitorAlertDocument.FromIncident)],
            TotalCount = incidents.Count()
        };
    }

    private static List<string> MapToAzMonitorStatuses(IEnumerable<string>? backendStatuses)
    {
        if (backendStatuses == null || !backendStatuses.Any())
            return [];

        var lowerStatuses = backendStatuses.Select(s => s.ToLower()).ToHashSet();

        // If any of resolved, mitigated, or closed are present, return both Acknowledged and Closed once
        if (lowerStatuses.Contains("resolved") || lowerStatuses.Contains("mitigated") || lowerStatuses.Contains("closed"))
        {
            return ["Acknowledged", "Closed"];
        }

        return [.. backendStatuses];
    }
}
