// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Data.DataModels.IncidentModel;
using Agent.Data.Interface.IncidentAPI;
using Agent.Runtime.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.Scanner;

public class AzMonitorScanner(
    CosmosClient cosmosClient,
    CosmosDBSettings cosmosDbSettings,
    IIncidentFilterManagementService<AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload> incidentFilterManagementService,
    IIncidentHandlingService<AzMonitorIncidentFilterDocumentPayload> incidentHandlingService,
    ILogger<AzMonitorScanner> logger,
    IAzMonitorAlertService azMonitorAlertService,
    IIncidentStatusMetricsService incidentsStatusMetricsService,
    IAgentOutboundCommunicationService outboundCommunicationService
        ) : IncidentScannerBase<AzMonitorAlertDocument, AlertItem, AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload>(
        cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName),
        incidentFilterManagementService,
        logger)
{
    private readonly IAzMonitorAlertService _azMonitorAlertService = azMonitorAlertService;
    private readonly uint pageSize = 10;
    private readonly IIncidentHandlingService<AzMonitorIncidentFilterDocumentPayload> _incidentHandlingService = incidentHandlingService;
    private readonly IIncidentStatusMetricsService _incidentsStatusMetricsService = incidentsStatusMetricsService;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService = outboundCommunicationService;

    private DateTimeOffset ParseDateTimeOffset(string? value)
    {
        DateTimeOffset createdAt;
        if (!string.IsNullOrEmpty(value) && DateTimeOffset.TryParse(value, out var parsedDate))
        {
            createdAt = parsedDate;
        }
        else
        {
            createdAt = DateTimeOffset.UtcNow;
            _logger.LogInternalWarning($"Could not parse start time {value}, using current time instead");
        }

        return createdAt;
    }

    protected override string GetIncidentId(AlertItem incident)
    {
        return incident.Id;
    }

    protected override async Task NotifyUserIfNeededAsync(AzMonitorAlertDocument incidentDocument, AlertItem incident, AzMonitorIncidentFilterDocument filter, List<string> relatedResourceIds)
    {
        IncidentHandlingRequestModel<AzMonitorIncidentFilterDocumentPayload> request = new()
        {
            IncidentId = incident.Id,
            Title = incident.Name,
            Severity = incident.Properties.Essentials?.Severity ?? string.Empty,
            IncidentFilter = filter,
            IncidentHandler = null,
            CreatedTime = ParseDateTimeOffset(incident.Properties.Essentials?.StartDateTime),
            ImpactedService = string.Empty
        };
        await _incidentHandlingService.HandleIncidentAsync(request);
    }

    /// <summary>
    /// Periodically refresh incident metrics.
    /// </summary>
    /// <returns></returns>
    protected override async Task PostScanningAsync()
    {
        var incidentMetrics = await _incidentsStatusMetricsService.GetIncidentStatusMetricsAsync(null, DateTime.Now);
        await _outboundCommunicationService.NotifyIncidentStatusMetrics(Guid.Empty, incidentMetrics);
    }

    protected override async Task<IEnumerable<AlertItem>> ScanIncidentsForFilter(AzMonitorIncidentFilterDocument filter, CancellationToken cancellationToken)
    {
        var incidents = new List<AlertItem>();
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInternalInformation("Cancellation requested, stopping the ServiceNow scanner.");
            return incidents;
        }

        try
        {
            uint page = 0;
            while (true)
            {
                var offset = page * pageSize;
                var result = await _azMonitorAlertService.GetIncidentsAsync(pageSize, offset, lastScanTime, filter);
                if (result is null || !result.Any())
                {
                    break;
                }
                incidents.AddRange(result);
                page++;
            }

        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error scanning incidents for filter {filterId}", filter.Id);
        }
        return incidents;
    }

    /// <summary>
    /// Placeholder for AzMonitor Alert, since alert will get updated in later NotifyUserIfNeededAsync
    /// </summary>
    /// <param name="incidentDocument"></param>
    /// <param name="incident"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected override Task<AzMonitorAlertDocument> UpsertIncidentDocumentIfNeededAsync(AzMonitorAlertDocument? incidentDocument, AlertItem incident, CancellationToken cancellationToken = default)
    {
        if (incidentDocument is not null)
        {
            return Task.FromResult(incidentDocument);
        }
        else
        {
            return Task.FromResult(AzMonitorAlertDocument.FromIncident(incident));
        }
    }
}
