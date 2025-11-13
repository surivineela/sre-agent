// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Data.DataModels.IncidentModel;
using Agent.Data.Helpers;
using Agent.Data.Interface.IncidentAPI;
using Agent.Runtime.Services;
using Azure.Core;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
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
    IAgentOutboundCommunicationService outboundCommunicationService,
    IIncidentAnalysisService<AzMonitorAlertDocument, AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload, AlertItem> incidentAnalysisService
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

    protected override string GetIncidentId(AlertItem incident)
    {
        return new ResourceIdentifier(incident.Id).Name ?? incident.Id;
    }

    protected override async Task NotifyUserIfNeededAsync(AzMonitorAlertDocument incidentDocument, AlertItem incident, AzMonitorIncidentFilterDocument filter, List<string> relatedResourceIds)
    {
        // if incident is already resolved or closed, no need to handle it
        string incidentStatus = incidentDocument.Status.ToLower();
        if (string.Equals(incidentStatus, AzMonitorIncidentStatus.Closed.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(incidentStatus, AzMonitorIncidentStatus.Resolved.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var incidentId = GetIncidentId(incident);
        var threadDocument = await GetIncidentThread(incidentId);
        if (threadDocument is null)
        {
            var request = new IncidentHandlingRequestModelWithFilterOnly<AzMonitorIncidentFilterDocumentPayload>()
            {
                IncidentId = incident.Id,
                Title = incident.Name,
                Severity = incident.Properties.Essentials?.Severity ?? string.Empty,
                CreatedTime = DateTimeHelper.ParseDateTimeOffset(incident.Properties.Essentials?.StartDateTime).UtcDateTime,
                ImpactedService = string.Empty,
                IncidentFilter = filter
            };
            await _incidentHandlingService.HandleIncidentAsync(request);
        }
        else
        {
            // could update thread with any new incident info
            return;
        }

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

    protected override IncidentManagementType GetIncidentManagementType()
    {
        return IncidentManagementType.AzMonitor;
    }

    protected override async Task<IEnumerable<AlertItem>> ScanIncidentsForFilter(AzMonitorIncidentFilterDocument filter, CancellationToken cancellationToken)
    {
        var incidents = new List<AlertItem>();
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInternalInformation("Cancellation requested, stopping the AzMonitor scanner.");
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
    protected override async Task<AzMonitorAlertDocument> UpsertIncidentDocumentIfNeededAsync(AzMonitorAlertDocument? incidentDocument, AlertItem incident, AzMonitorIncidentFilterDocument? filterDocument, CancellationToken cancellationToken = default)
    {
        try
        {
            if (incidentDocument is null)
            {
                logger.LogInternalInformation("[AzMonitorAlertScanner] Creating new incident document for AzMonitor by id {incidentId}", incident.Id);

                incidentDocument = AzMonitorAlertDocument.FromIncident(incident);

                await _container.CreateItemAsync(incidentDocument, new PartitionKey(incidentDocument.PartitionKey), cancellationToken: cancellationToken);
                logger.LogInternalInformation("[AzMonitorAlertScanner] Created new incident document for AzMonitor incident {incidentId}", incident.Id);
            }
            else if (string.Equals(incidentDocument.AlertId, incident.Id, StringComparison.OrdinalIgnoreCase))
            {
                var updatedDoc = AzMonitorAlertDocument.FromIncident(incident);
                updatedDoc = updatedDoc with
                {
                    AIRootCause = incidentDocument.AIRootCause,
                    RootCauseDescription = incidentDocument.RootCauseDescription,
                    GeneralSummary = incidentDocument.GeneralSummary,
                    IsAssistedByAgent = incidentDocument.IsAssistedByAgent,
                    Tags = incidentDocument.Tags,
                    ResolvedAt = incidentDocument.ResolvedAt,
                    HitCount = incidentDocument.HitCount,
                    UserInputRequested = incidentDocument.UserInputRequested,
                    TargetResourceInputRequested = incidentDocument.TargetResourceInputRequested,
                };

                // Once incident is mitigated or resolved, do AI analysis
                string incidentStatus = updatedDoc.Status.ToLower();
                if ((string.Equals(incidentStatus, AzMonitorIncidentStatus.Closed.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(incidentStatus, AzMonitorIncidentStatus.Resolved.ToString(), StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(updatedDoc.AIRootCause) || string.IsNullOrWhiteSpace(updatedDoc.RootCauseDescription) || string.IsNullOrWhiteSpace(updatedDoc.GeneralSummary)))
                {
                    // if resolved by itself without agent action, set the value
                    if (updatedDoc.ResolvedAt == null)
                    {
                        updatedDoc.ResolvedAt = DateTime.UtcNow;
                    }

                    try
                    {
                        updatedDoc = await incidentAnalysisService.AnalyzeIncident(updatedDoc, incident, filterDocument);
                    }
                    catch (Exception ex)
                    {
                        logger.LogInternalError($"[AzMonitorAlertScanner] Error generating AI-generated insights for incident; {ex.Message}");
                    }
                }


                if (updatedDoc.LastModifiedTime > incidentDocument.LastModifiedTime)
                {
                    try
                    {
                        await incidentAnalysisService.Ingest(updatedDoc, filterDocument);
                    }
                    catch (Exception ex)
                    {
                        logger.LogInternalError(ex, "[AzMonitorAlertScanner] Error during ingestion of AzMonitor incident {incidentId} data into App Insights", incident.Id);
                    }
                }

                logger.LogInternalInformation("[AzMonitorAlertScanner] Upserting existing incident document for AzMonitor incident {incidentId}", incident.Id.ToString());
                var response = await _container.UpsertItemAsync(updatedDoc, new PartitionKey(updatedDoc.PartitionKey), cancellationToken: cancellationToken);
                incidentDocument = response.Resource;
            }

            if (incidentDocument == null)
            {
                throw new Exception($"Failed to create or update incident document for AzMonitor incident {incident?.Id}. The incident document is null.");
            }

            return incidentDocument;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[AzMonitorAlertScanner] Error upserting incident document for AzMonitor incident {incidentId}", incident.Id);
            throw;
        }
    }

    private async Task<ThreadDocument?> GetIncidentThread(string incidentId)
    {
        var threads = _container.GetItemLinqQueryable<ThreadDocument>()
            .Where(doc => doc.DocumentType == "Thread" && doc.Source == ThreadSource.Incident)
            .Where(doc => doc.IncidentSource != null && doc.IncidentSource.IncidentType == Agent.Core.Models.Api.v1.IncidentType.AzMonitor && doc.IncidentSource.IncidentId == incidentId || doc.IncidentId == incidentId)
            .OrderBy(doc => doc.CreatedTimestamp)
            .ToFeedIterator();

        if (threads.HasMoreResults)
        {
            var response = await threads.ReadNextAsync();
            if (response.Count == 1)
            {
                return response.FirstOrDefault();
            }
            else if (response.Count > 1)
            {
                logger.LogInternalWarning("[IcmScanner] Multiple threads({threadIds}) found for incident {incidentId}, returning the first one.", string.Join(',', response.Select(t => t.Id)), incidentId);
                return response.FirstOrDefault();
            }
        }
        return null;
    }

}
