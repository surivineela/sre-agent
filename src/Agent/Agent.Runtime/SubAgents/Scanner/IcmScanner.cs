// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Logging;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents.IcmScanner;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.AzureAd.Icm.Types;
using Microsoft.Extensions.Logging;
using Microsoft.SREAgent.Incidents.IcM.Model;
using IncidentStatus = Microsoft.AzureAd.Icm.Types.IncidentStatus;

namespace Agent.Runtime.SubAgents.Scanner;

/// <summary>
/// Represents the information logged when an incident is resolved or mitigated
/// </summary>
public record IncidentResolveActionData
{
    public string IncidentSource { get; init; } = string.Empty;
    public string IncidentId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ResolvedBy { get; init; } = string.Empty;
    public string AgentMode { get; init; } = string.Empty;
}

public class IcmScanner(ILogger<IcmScanner> logger,
    IICMAPIClient icmApiClient,
    CosmosClient cosmosClient,
    CosmosDBSettings cosmosDbSettings,
    IIncidentHandlingService<IcmIncidentFilterDocumentPayload> incidentHandlingService,
    IIncidentManagementService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload> incidentManagementService,
    IIncidentFilterManagementService<IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload> incidentFilterManagementService,
    IAgentInboundCommunicationService agentInboundCommunicationService,
    IncidentManagementSettings incidentManagementSettings,
    IIncidentAnalysisService<IcmIncidentDocument, IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload, ICMIncident> incidentAnalysisService) : IIncidentScanner
{

    private readonly Container container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
    private const uint PageSize = 50;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(1);
    private bool isScanSucceeded = true;
    private DateTime lastScanTime;
    private DateTime? latestModifiedDateInScan = null;
    //After offset > 5000, ICM endpoint will returning 400 bad request
    //Updating offset to 200, since now it will apply on every existing incident Filter
    private static readonly int maxOffset = 200;

    // Automated RCA configuration
    private bool IsAutomatedRCAEnabled => incidentManagementSettings.AutomatedRCA.Enabled;
    private string WebBaseUrl => incidentManagementSettings.AutomatedRCA.WebBaseUrl;
    private bool IsICMAPIReadOnly => incidentManagementSettings.ICMAPI.ReadOnly;

    // Track processed incidents to avoid duplicate processing
    private readonly HashSet<string> _processedIncidents = new();
    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        var lastScanTimeKey = LastScanTimeDoc.GetLastScanTimeKey(IncidentManagementType.Icm);
        var lastScanTimeDoc = await GetDocumentAsync<LastScanTimeDoc>(lastScanTimeKey, lastScanTimeKey);
        lastScanTime = DateTime.UtcNow.AddDays(-30); // Default to 30 days ago

        if (lastScanTimeDoc != null)
        {
            if (lastScanTimeDoc.LastScanTime == DateTime.MinValue)
            {
                logger.LogInternalWarning("[IcmScanner] Last scan time document has MinValue, ignoring and using default 30 days ago.");
            }
            else
            {
                lastScanTime = lastScanTimeDoc.LastScanTime;
                logger.LogInternalInformation("[IcmScanner] Retrieved last scan time from document: {lastScanTime}", lastScanTime);
            }
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            // Drain test queue (local dev) before normal scanning if enabled.
            if (IcmScannerTestQueueHelper.IsEnabled())
            {
                try
                {
                    await ProcessTestQueueAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogInternalError(ex, "[IcmScanner] Error processing test queue batch");
                }
            }
            var filters = await incidentFilterManagementService.ListIncidentFilters(false);
            var scanStartTime = DateTime.UtcNow;
            if (filters is null || filters.Count == 0)
            {
                logger.LogInternalInformation("[IcmScanner] No incident filters found, skipping IcM scanner.");
            }
            else
            {
                logger.LogInternalInformation("[IcmScanner] Found {filterCount} incident filters, starting IcM scanner.", filters.Count);
                latestModifiedDateInScan = lastScanTime; // Reset for each scan cycle
                await ScanAllIncidentsAsync(filters, cancellationToken);
                if (isScanSucceeded)
                {
                    if (latestModifiedDateInScan.HasValue)
                    {
                        // Use the latest ModifiedDate from incidents fetched in this scan as the new checkpoint
                        // This ensures we don't miss incidents due to API lag or timing issues
                        lastScanTime = await UpdateLastScanTimeDocAsync(latestModifiedDateInScan.Value, IncidentManagementType.Icm);
                        logger.LogInternalInformation("[IcmScanner] Updated checkpoint to latest incident ModifiedDate: {lastScanTime}", lastScanTime);

                        if (lastScanTime == DateTime.MinValue)
                        {
                            logger.LogInternalWarning($"[IcmScanner] Last scan time updated to MinValue, resetting to {latestModifiedDateInScan.Value}");
                            lastScanTime = latestModifiedDateInScan.Value;
                        }
                    }
                    else
                    {
                        logger.LogInternalInformation("[IcmScanner] No incidents found in this scan, lastScanTime remains at: {lastScanTime}", lastScanTime);
                    }
                }
                else
                {
                    logger.LogInternalWarning("[IcmScanner] IcM scanner failed to scan incidents, last scan time will not be updated.");
                }
            }
            await Task.Delay(ScanInterval, cancellationToken);
        }
    }

    private async Task ScanAllIncidentsAsync(List<IcmIncidentFilterDocument> filters, CancellationToken cancellationToken)
    {
        foreach (var filter in filters)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInternalInformation("[IcmScanner] Cancellation requested, stopping the IcM scanner.");
                return;
            }
            if (filter is IcmIncidentFilterDocument filterDocument && filterDocument.DocumentType == "IncidentFilterIcm")
            {
                try
                {
                    await ScanIncidentsForFilter(filterDocument, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogInternalError(ex, "[IcmScanner] Error scanning incidents for filter: {filterId}", filterDocument.Id);
                }
            }
        }
    }

    private async Task ScanIncidentsForFilter(IcmIncidentFilterDocument filterDocument, CancellationToken cancellationToken)
    {
        logger.LogInternalInformation("[IcmScanner] Scanning incidents for filter: {filterId}", filterDocument.Id);
        uint page = 0;
        isScanSucceeded = true;
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                isScanSucceeded = false;
                logger.LogInternalInformation("[IcmScanner] Cancellation requested, stopping the IcM scanner.");
                return;
            }
            var offset = page * PageSize;

            if (offset > maxOffset)
            {
                logger.LogInternalInformation("[IcmScanner] Stop scanning ICMs over {offset}", offset);
                return;
            }

            try
            {
                logger.LogInternalInformation("[IcmScanner] Scanning IcM incidents, page {page}, lastScanTime {lastScanTime}, filter: {filterId}", page, lastScanTime, filterDocument.Id);

                // Use OwningTeamId and IncidentType filtering if available
                var incidents = await icmApiClient.GetIncidentsAsync(
                    PageSize,
                    offset,
                    lastScanTime,
                    null,
                    filterDocument.TitleContains,
                    string.IsNullOrWhiteSpace(filterDocument.OwningTeamId) ? null : filterDocument.OwningTeamId,
                    string.IsNullOrWhiteSpace(filterDocument.IncidentType) ? null : filterDocument.IncidentType,
                    string.IsNullOrWhiteSpace(filterDocument.CreatedBy) ? null : filterDocument.CreatedBy,
                    string.IsNullOrWhiteSpace(filterDocument.MonitorId) ? null : filterDocument.MonitorId,
                    string.IsNullOrWhiteSpace(filterDocument.Priority) ? null : filterDocument.Priority
                );

                if (incidents is null || incidents.Count == 0)
                {
                    logger.LogInternalInformation("[IcmScanner] No incidents found for filter: {filterId}", filterDocument.Id);
                    return;
                }

                // Track the latest ModifiedDate from this batch of incidents
                foreach (var incident in incidents)
                {
                    // Update the latest ModifiedDate seen in this scan
                    // Convert DateTimeOffset to UTC DateTime for consistent checkpoint tracking
                    var incidentModifiedDate = incident.LastModifiedDate.UtcDateTime;
                    if (!latestModifiedDateInScan.HasValue || incidentModifiedDate > latestModifiedDateInScan.Value)
                    {
                        latestModifiedDateInScan = incidentModifiedDate;
                    }

                    var incidentDocument = await GetDocumentAsync<IcmIncidentDocument>(incident.Id.ToString(), incident.Id.ToString());
                    var existingLastModifiedTime = incidentDocument != null ? incidentDocument.UpdatedAt : DateTime.MinValue;
                    incidentDocument = await UpsertIncidentDocumentIfNeededAsync(incidentDocument, incident, filterDocument);

                    // ingest incident data into App Insights if handled already. First ingestion should be upon handling, as executed in NotifyUser
                    var threadDocument = await GetIncidentThread(incidentDocument.Id.ToString());
                    if (threadDocument != null && incidentDocument.UpdatedAt > existingLastModifiedTime)
                    {
                        try
                        {
                            await incidentAnalysisService.Ingest(incidentDocument, filterDocument);
                        }
                        catch (Exception ex)
                        {
                            logger.LogInternalError(ex, "[IcmScanner] Failed to ingest incident data into App Insights");
                        }
                    }

                    if (!await isIncidentNeedToHandle(incident))
                    {
                        logger.LogInternalInformation("[IcmScanner] Incident {incidentId} does not need to be handled.", incident.Id);
                        continue;
                    }

                    // Process team-specific incidents for automated RCA when OwningTeamId is set
                    if (!string.IsNullOrWhiteSpace(filterDocument.OwningTeamId) && IsAutomatedRCAEnabled)
                    {
                        await ProcessTeamSpecificIncident(incidentDocument, filterDocument);
                    }
                    else
                    {
                        // Traditional incident handling for regular filters
                        await NotifyUserAsync(incidentDocument, new List<string>(), filterDocument);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "[IcmScanner] Error scanning IcM incidents");
                isScanSucceeded = false;
            }
            page++;
        }
    }

    /// <summary>
    /// Monitors RCA completion and posts results back to ICM
    /// </summary>
    /// <param name="incidentId">The incident ID</param>
    /// <param name="threadId">The thread ID where RCA is running</param>
    /// <param name="threadUrl">The URL to the RCA thread</param>
    private async Task MonitorRCACompletionAsync(string incidentId, Guid threadId, string threadUrl, string owningTeamId)
    {
        try
        {
            // Monitor for up to 24 hours, checking every 30 minutes
            var maxAttempts = 48;
            var checkInterval = TimeSpan.FromMinutes(30);

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                await Task.Delay(checkInterval);

                try
                {
                    // Prefer tag-based completion check
                    var incident = await icmApiClient.GetIncidentAsync(incidentId);
                    var teamCompletedTag = !string.IsNullOrWhiteSpace(owningTeamId) ? $"{owningTeamId}:Completed" : null;
                    if (!string.IsNullOrWhiteSpace(teamCompletedTag) &&
                        incident?.Tags?.Any(t => string.Equals(t, teamCompletedTag, StringComparison.OrdinalIgnoreCase)) == true)
                    {
                        logger.LogInternalInformation("[IcmScanner] RCA completed for incident {incidentId} (found tag {tag})", incidentId, teamCompletedTag);
                        _processedIncidents.Remove(incidentId);
                        return;
                    }

                    // Legacy: context-state based (may never reach Completed; keep as secondary)
                    var agentContexts = await GetAgentContextsForThread(threadId);
                    var activeContext = agentContexts?.FirstOrDefault();
                    if (activeContext == null)
                    {
                        logger.LogInternalWarning("[IcmScanner] No agent context found for thread {threadId}, attempt {attempt}", threadId, attempt + 1);
                        continue;
                    }

                    if (activeContext.ContextState == ContextStateEnum.Failed)
                    {
                        logger.LogInternalWarning("[IcmScanner] RCA encountered error for incident {incidentId}", incidentId);
                        _processedIncidents.Remove(incidentId);
                        return;
                    }

                    logger.LogInternalInformation("[IcmScanner] RCA still in progress for incident {incidentId}, state: {state}, attempt {attempt}",
                        incidentId, activeContext.ContextState, attempt + 1);
                }
                catch (Exception ex)
                {
                    logger.LogInternalWarning(ex, "[IcmScanner] Error checking RCA completion status for incident {incidentId}, attempt {attempt}",
                        incidentId, attempt + 1);
                    _processedIncidents.Remove(incidentId);
                }
            }

            // Timeout
            logger.LogInternalWarning("[IcmScanner] RCA monitoring timed out for incident {incidentId}", incidentId);
            _processedIncidents.Remove(incidentId);
            var timeoutMessage = $"⏰ **RCA Analysis Status**: The automated analysis is taking longer than expected. Please check the [analysis thread]({threadUrl}) for the latest progress.";
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[IcmScanner] Error monitoring RCA completion for incident {incidentId}", incidentId);
        }
    }

    private async Task<T?> GetDocumentAsync<T>(string id, string partitionKey) where T : ICosmosDocument
    {
        try
        {
            var response = await container.ReadItemAsync<T>(
                id,
                new PartitionKey(partitionKey)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }
        catch (JsonException ex) when (ex.Message.StartsWith("The JSON value could not be converted to") && typeof(T) == typeof(IcmIncidentDocument))
        {
            logger.LogInternalWarning("[IcmScanner] JSON deserialization error for document Id: {id} PartitionKey: {partitionKey}, Prepare to delete Document.Message: {message}", id, partitionKey, ex.Message);
            await container.DeleteItemAsync<T>(
                id,
                new PartitionKey(partitionKey)
            );
            return default;
        }
    }

    private async Task<IcmIncidentDocument> UpsertIncidentDocumentIfNeededAsync(IcmIncidentDocument? incidentDocument, ICMIncident incident, IcmIncidentFilterDocument? filterDocument, CancellationToken cancellationToken = default)
    {
        try
        {
            if (incidentDocument is null)
            {
                logger.LogInternalInformation("[IcmScanner] Creating new incident document for IcM by id {incidentId}", incident.Id);

                incidentDocument = new IcmIncidentDocument(incident);

                await container.CreateItemAsync(incidentDocument, new PartitionKey(incidentDocument.PartitionKey), cancellationToken: cancellationToken);
                logger.LogInternalInformation("[IcmScanner] Created new incident document for IcM incident {incidentId}", incident.Id);
            }
            else if (incidentDocument.Id == incident.Id.ToString())
            {
                //var patchOperationList = new List<PatchOperation>();
                //// PatchOperation.Add is used to update existing fields or add new fields if they don't exist.
                //// Current is incorrect! PatchPath should be aligin with serialized string, which first character is lowercase
                //// https://learn.microsoft.com/en-us/azure/cosmos-db/partial-document-update
                //if (string.IsNullOrEmpty(incident.Title) && incidentDocument.Title != incident.Title)
                //{
                //    patchOperationList.Add(PatchOperation.Add($"/{nameof(IcmIncidentDocument.Title)}", incident.Title));
                //}

                //if (string.IsNullOrEmpty(incident.Summary) && incidentDocument.Description != incident.Summary)
                //{
                //    patchOperationList.Add(PatchOperation.Add($"/{nameof(IcmIncidentDocument.Description)}", incident.Summary));
                //}

                //if (incidentDocument.Status != incident.Status.ToString())
                //{
                //    patchOperationList.Add(PatchOperation.Add($"/{nameof(IcmIncidentDocument.Status)}", incident.Status.ToString()));
                //}

                //if (incidentDocument.Priority != incident.Severity)
                //{
                //    patchOperationList.Add(PatchOperation.Add($"/{nameof(IcmIncidentDocument.Priority)}", incident.Severity));
                //}
                //if(patchOperationList.Count >= 0)
                //{
                //    logger.LogInternalInformation("Updating existing incident document for Icm incident {incidentId}", incident.IncidentId);
                //    patchOperationList.Add(PatchOperation.Add($"/{nameof(IcmIncidentDocument.UpdatedAt)}", DateTime.UtcNow));
                //    incidentDocument = await container.PatchItemAsync<IcmIncidentDocument>(incidentDocument.Id, new PartitionKey(incidentDocument.PartitionKey), patchOperationList, cancellationToken: cancellationToken);
                //}

                //For now use UpsertItemAsync for updating IcmIncidentDocument with latest non-critical fields, later can switch to PatchItemAsync if needed.
                var updatedDoc = new IcmIncidentDocument(incident)
                {
                    AIRootCause = incidentDocument.AIRootCause,
                    RootCauseDescription = incidentDocument.RootCauseDescription,
                    GeneralSummary = incidentDocument.GeneralSummary,
                    IsAssistedByAgent = incidentDocument.IsAssistedByAgent,
                    DiscussionEntries = incidentDocument.DiscussionEntries
                };

                // Once incident is mitigated or resolved, do AI analysis
                var incidentStatus = incident.State.ToString();
                if ((string.Equals(incidentStatus, IncidentStatus.Mitigated.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(incidentStatus, IncidentStatus.Resolved.ToString(), StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(updatedDoc.AIRootCause) || string.IsNullOrWhiteSpace(updatedDoc.RootCauseDescription) || string.IsNullOrWhiteSpace(updatedDoc.GeneralSummary)))
                {
                    try
                    {
                        updatedDoc = await incidentAnalysisService.AnalyzeIncident(updatedDoc, incident, filterDocument);
                    }
                    catch (Exception ex)
                    {
                        logger.LogInternalError($"[IcmScanner] Error generating AI-generated insights for incident; {ex.Message}");
                    }
                }

                var latestDiscussionEntries = await icmApiClient.GetIncidentDiscussionEntriesAsync(incident.Id.ToString());
                updatedDoc.DiscussionEntries = latestDiscussionEntries;
                var newNotes = latestDiscussionEntries?
                        .Where(entry => entry.Date > lastScanTime.AddMinutes(-5)) // Add 5 minutes buffer to avoid missing notes due to time skew
                        .Select(entry => new IncidentDiscussion($"{entry.DescriptionEntryId}-{entry.Date}", entry.Text, entry.ChangedBy, entry.ChangedBy, entry.Date))
                        .ToList() ?? new List<IncidentDiscussion>();

                if (newNotes.Count > 0)
                {
                    // Check if agent assisted with incident based on new notes
                    if (!updatedDoc.IsAssistedByAgent)
                    {
                        var agentAssisted = await incidentAnalysisService.DetermineAgentAssistanceFromNotes(updatedDoc, newNotes);
                        if (agentAssisted)
                        {
                            logger.LogInternalInformation("[IcmScanner] Detected agent assistance for incident {incidentId}, updating document", updatedDoc.Id);
                            updatedDoc.IsAssistedByAgent = true;
                        }
                    }
                }

                logger.LogInternalInformation("[IcmScanner] Upserting existing incident document for IcM incident {incidentId}", incident.Id.ToString());
                var response = await container.UpsertItemAsync(updatedDoc, new PartitionKey(updatedDoc.PartitionKey), cancellationToken: cancellationToken);
                incidentDocument = response.Resource;
            }

            if (incidentDocument == null)
            {
                throw new Exception($"Failed to create or update incident document for IcM incident {incident?.Id}. The incident document is null.");
            }

            return incidentDocument;

        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.RequestEntityTooLarge)
        {
            logger.LogInternalWarning("[IcmScanner] Original IcM is too large, truncate incident details");
            incidentDocument = IcmIncidentDocument.TruncateIcmIncidentDocument(incident);
            await container.UpsertItemAsync(incidentDocument, new PartitionKey(incidentDocument.PartitionKey), cancellationToken: cancellationToken);
            return incidentDocument;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[IcmScanner] Error upserting incident document for IcM incident {incidentId}", incident.Id);
            throw;
        }
    }

    private async Task NotifyUserAsync(IcmIncidentDocument incidentDocument, List<string> relatedResourceIds, IcmIncidentFilterDocument? filterDocument)
    {
        try
        {
            if (incidentDocument is null)
            {
                logger.LogInternalWarning("[IcmScanner] Incident document is null, skipping notification.");
                return;
            }

            if (incidentDocument.State.ToString().Equals("resolved", StringComparison.OrdinalIgnoreCase) || incidentDocument.State.ToString().Equals("mitigated", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInternalInformation("[IcmScanner] Incident {incidentId} is mitigated/resolved, updating thread status if exists.", incidentDocument.Id);

                // Update thread status if exists
                try
                {
                    var needsUpsertForDetailsChange = false;
                    var needsUpsertForResolvedStatus = false;
                    var existingThreadDocument = await GetIncidentThread(incidentDocument.Id);
                    if (existingThreadDocument is not null)
                    {
                        if (existingThreadDocument.IncidentDetails != null)
                        {
                            if (existingThreadDocument.IncidentDetails.IncidentTitle != incidentDocument.Title || existingThreadDocument.IncidentDetails.IncidentPriority != incidentDocument.Priority)
                            {
                                var updatedIncidentDetails = new IncidentDetails(
                                    incidentDocument.Title,
                                    existingThreadDocument.IncidentDetails.IncidentCreatedTime,
                                    incidentDocument.Priority,
                                    existingThreadDocument.IncidentDetails.ImpactedService,
                                    existingThreadDocument.IncidentDetails.FilterId,
                                    existingThreadDocument.IncidentDetails.HandlerId,
                                    InvestigationStatus.Complete);
                                existingThreadDocument.IncidentDetails = updatedIncidentDetails;
                                needsUpsertForDetailsChange = true;
                            }
                        }

                        var newStatus = incidentDocument.Status.ToString().Equals("mitigated", StringComparison.OrdinalIgnoreCase) ? "mitigated" : "resolved";
                        if (existingThreadDocument.IncidentStatus != newStatus)
                        {
                            // Log agent action event for incident resolution/mitigation only when status changes
                            try
                            {
                                // Determine if the incident was resolved by Agent or User based on tags
                                var resolvedBy = "User"; // Default to User
                                if (incidentDocument.Tags?.Any(tag => tag.Equals("SREAgent_Processed", StringComparison.OrdinalIgnoreCase) ||
                                                                     tag.Equals("SREAgent_Mitigated", StringComparison.OrdinalIgnoreCase)) == true)
                                {
                                    resolvedBy = "Agent";
                                }

                                var resolveActionData = new IncidentResolveActionData
                                {
                                    IncidentSource = "Icm",
                                    IncidentId = incidentDocument.Id,
                                    Status = incidentDocument.State,
                                    ResolvedBy = resolvedBy
                                };

                                logger.LogAgentAction(
                                    action: AgentActionEvents.ResolveIncident,
                                    parameter: JsonSerializer.Serialize(resolveActionData),
                                    status: AgentActionStatus.Success,
                                    duration: 0,
                                    threadId: existingThreadDocument.Id);

                                logger.LogInternalInformation("[IcmScanner] Logged ResolveIncident action for incident {incidentId} with status {status} resolved by {resolvedBy}",
                                                            incidentDocument.Id, incidentDocument.State, resolvedBy);
                            }
                            catch (Exception ex)
                            {
                                logger.LogInternalError(ex, "[IcmScanner] Failed to log ResolveIncident action for incident {incidentId}", incidentDocument.Id);
                            }

                            existingThreadDocument.IncidentStatus = newStatus;
                            if (existingThreadDocument.IncidentDetails != null)
                            {
                                var updatedIncidentDetails = new IncidentDetails(
                                existingThreadDocument.IncidentDetails.IncidentTitle,
                                existingThreadDocument.IncidentDetails.IncidentCreatedTime,
                                existingThreadDocument.IncidentDetails.IncidentPriority,
                                existingThreadDocument.IncidentDetails.ImpactedService,
                                existingThreadDocument.IncidentDetails.FilterId,
                                existingThreadDocument.IncidentDetails.HandlerId,
                                InvestigationStatus.Complete);
                                existingThreadDocument.IncidentDetails = updatedIncidentDetails;
                            }
                            needsUpsertForResolvedStatus = true;
                        }

                        if (needsUpsertForDetailsChange || needsUpsertForResolvedStatus)
                        {
                            if (needsUpsertForDetailsChange)
                            {
                                logger.LogInternalInformation("[IcmScanner] Title or priority changed for ICM incident {incidentId}", incidentDocument.Id);
                                logger.LogInternalInformation("[IcmScanner] Updating incident title or priority on ICM incident thread {threadId}", existingThreadDocument.Id);
                            }
                            if (needsUpsertForResolvedStatus)
                            {
                                logger.LogInternalInformation("[IcmScanner] Updating thread status to {status} for ICM incident {incidentId}", newStatus, incidentDocument.Id);
                            }

                            await container.UpsertItemAsync(existingThreadDocument, new PartitionKey(existingThreadDocument.Id));

                            if (needsUpsertForDetailsChange)
                            {
                                logger.LogInternalInformation("[IcmScanner] Updated incident title or priority on ICM incident thread {threadId}", existingThreadDocument.Id);
                            }
                            if (needsUpsertForResolvedStatus)
                            {
                                logger.LogInternalInformation("[IcmScanner] Updated thread status to {status} for ICM incident {incidentId}", newStatus, incidentDocument.Id);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogInternalError(ex, "[IcmScanner] Error updating thread status for ICM incident {incidentId}", incidentDocument.Id);
                }
                return;
            }

            var threadDocument = await GetIncidentThread(incidentDocument.Id.ToString());
            if (threadDocument is null)
            {
                if (filterDocument is null)
                {
                    logger.LogInternalWarning("[IcmScanner] Filter document is null, cannot create thread for incident {incidentId}", incidentDocument.Id);
                    return;
                }
                logger.LogInternalInformation("[IcmScanner] Thread doesn't exist for incident {incidentId} by filter {filterId}, creating new thread", incidentDocument.Id, filterDocument.Id);

                var response = await incidentHandlingService.HandleIncidentAsync(new IncidentHandlingRequestModelWithFilterOnly<IcmIncidentFilterDocumentPayload>()
                {
                    IncidentId = incidentDocument.Id.ToString(),
                    Title = incidentDocument.Title,
                    Description = incidentDocument.Description,
                    Severity = incidentDocument.Priority,
                    CreatedTime = incidentDocument.CreatedAt,
                    ImpactedService = incidentDocument.ImpactedServiceName,
                    IncidentFilter = filterDocument
                });
            }
            else
            {
                logger.LogInternalInformation("[IcmScanner] Thread already exists for incident {incidentId}, checking whether it needs to be updated", incidentDocument.Id);

                if (threadDocument.IncidentDetails != null)
                {
                    if (threadDocument.IncidentDetails.IncidentTitle != incidentDocument.Title || threadDocument.IncidentDetails.IncidentPriority != incidentDocument.Priority)
                    {
                        var updatedIncidentDetails = new IncidentDetails(
                            incidentDocument.Title,
                            threadDocument.IncidentDetails.IncidentCreatedTime,
                            incidentDocument.Priority,
                            threadDocument.IncidentDetails.ImpactedService,
                            threadDocument.IncidentDetails.FilterId,
                            threadDocument.IncidentDetails.HandlerId,
                            threadDocument.IncidentDetails.InvestigationStatus);
                        threadDocument.IncidentDetails = updatedIncidentDetails;
                        logger.LogInternalInformation("[IcmScanner] Title or priority changed for ICM incident {incidentId}", incidentDocument.Id);
                        logger.LogInternalInformation("[IcmScanner] Updating incident title or priority on ICM incident thread {threadId}", threadDocument.Id);
                        await container.UpsertItemAsync(threadDocument, new PartitionKey(threadDocument.Id));
                        logger.LogInternalInformation("[IcmScanner] Updated incident title or priority on ICM incident thread {threadId}", threadDocument.Id);
                    }
                }

                var existingIncidentDocument = await incidentManagementService.GetIncidentAsync(incidentDocument.Id.ToString(), false);
                var existingDiscussionEntries = existingIncidentDocument != null ? existingIncidentDocument.DiscussionEntries : new List<DescriptionEntry>();
                var latestDiscussionEntries = await icmApiClient.GetIncidentDiscussionEntriesAsync(incidentDocument.Id.ToString());

                var newNotes = latestDiscussionEntries
                        .Skip(existingDiscussionEntries.Count)
                        .Where(entry => entry.Date > lastScanTime)
                        .Select(entry => new IncidentDiscussion(incidentDocument.Id, entry.Text, entry.ChangedBy, entry.ChangedBy, entry.Date))
                        .ToList();

                if (newNotes.Count > 0)
                {
                    logger.LogInternalInformation("[IcmScanner] Found {newNotesCount} new notes for incident {incidentId}", newNotes.Count, incidentDocument.Id);
                    await agentInboundCommunicationService.AddNewDiscussionsToIncidentThread(Guid.Parse(threadDocument.Id), newNotes);
                    logger.LogInternalInformation("[IcmScanner] Added {newNotesCount} new notes to incident thread {threadId}", newNotes.Count, threadDocument.Id);
                }
                else
                {
                    logger.LogInternalInformation("[IcmScanner] No new notes found for incident {incidentId}", incidentDocument.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[IcmScanner] Error notifying user about incident {incidentId}", incidentDocument.Id);
        }
    }

    private async Task<ThreadDocument?> GetIncidentThread(string incidentId)
    {
        var threads = container.GetItemLinqQueryable<ThreadDocument>()
            .Where(doc => doc.DocumentType == "Thread" && doc.Source == ThreadSource.Incident)
            .Where(doc => (doc.IncidentSource != null && doc.IncidentSource.IncidentType == Agent.Core.Models.Api.v1.IncidentType.Icm && doc.IncidentSource.IncidentId == incidentId) || doc.IncidentId == incidentId)
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

    /// <summary>
    /// Team-specific incident processing (AutomatedRCA only)
    /// </summary>
    /// <param name="incidentDocument">Incident document</param>
    /// <param name="filterDocument">Filter document</param>
    private async Task ProcessTeamSpecificIncident(IcmIncidentDocument incidentDocument, IcmIncidentFilterDocumentPayload filterDocument, bool forceRun = false)
    {
        try
        {
            if (incidentDocument is null)
            {
                logger.LogInternalWarning("[IcmScanner] Incident document is null, skipping team-specific processing.");
                return;
            }

            // by-pass the checkinf for testing.
            if (forceRun)
            {
                logger.LogInternalWarning($"[IcmScanner] Rceive testing queue for IncidentId: {incidentDocument.Id} forceRun it.");
                await ExecuteAutomatedRCAAsync(incidentDocument, filterDocument.OwningTeamId);
                return;
            }

            // Skip resolved/mitigated incidents
            if (incidentDocument.State.ToString().Equals("resolved", StringComparison.OrdinalIgnoreCase) ||
                incidentDocument.State.ToString().Equals("mitigated", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInternalInformation("[IcmScanner] Incident {incidentId} is mitigated/resolved, skipping team-specific processing.", incidentDocument.Id);
                return;
            }

            // Check if already processed
            if (_processedIncidents.Contains(incidentDocument.Id.ToString()))
            {
                logger.LogInternalInformation("[IcmScanner] Incident {incidentId} already processed, skipping.", incidentDocument.Id);
                return;
            }

            // Get detailed information from ICM API and check tags
            var incident = await icmApiClient.GetIncidentAsync(incidentDocument.Id.ToString());
            if (incident == null)
            {
                logger.LogInternalWarning("[IcmScanner] Could not retrieve incident details for {incidentId}", incidentDocument.Id);
                return;
            }
            var owningTeamId = filterDocument.OwningTeamId;
            // New: Skip if team-specific Completed tag exists
            var teamCompletedTag = !string.IsNullOrWhiteSpace(owningTeamId) ? $"{owningTeamId}:Completed" : null;
            if (!string.IsNullOrWhiteSpace(teamCompletedTag) && incident.Tags?.Any(t => string.Equals(t, teamCompletedTag, StringComparison.OrdinalIgnoreCase)) == true)
            {
                logger.LogInternalInformation("[IcmScanner] Incident {incidentId} already completed for owning team (tag: {tag})", incidentDocument.Id, teamCompletedTag);
                return;
            }

            logger.LogInternalInformation("[IcmScanner] Incident {incidentId} qualifies for automated RCA execution via team filter", incidentDocument.Id);

            // Execute Automated RCA
            await ExecuteAutomatedRCAAsync(incidentDocument, owningTeamId);
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[IcmScanner] Error processing team-specific incident {incidentId}", incidentDocument.Id);
        }
    }

    /// <summary>
    /// Executes automated RCA for the given incident
    /// </summary>
    /// <param name="incidentDocument">The incident document to process</param>
    private async Task ExecuteAutomatedRCAAsync(IcmIncidentDocument incidentDocument, string owningTeamId)
    {
        try
        {
            logger.LogInternalInformation("[IcmScanner] Starting automated RCA execution for incident {incidentId}", incidentDocument.Id);

            // Mark incident as processed to avoid duplicate processing
            _processedIncidents.Add(incidentDocument.Id.ToString());

            // Begin scanner-origin scope so downstream components can gate ICM posting/tagging
            using (IncidentProcessingContext.BeginScannerOriginScope())
            {
                // Create thread that will trigger agent execution with the incident ID
                var (thread, agentContext) = await agentInboundCommunicationService.CreateAgentThread(
                    title: $"🤖 Automated RCA for ICM {incidentDocument.Id}: {incidentDocument.Title}",
                    message: $"Please analyze and route IncidentId {incidentDocument.Id} for RCA analysis.",
                    agentTypeEnum: AgentTypeEnum.Incident,
                    source: ThreadSource.Agent,
                    incidentId: incidentDocument.Id,
                    incidentSource: new IncidentSource(Agent.Core.Models.Api.v1.IncidentType.Icm, incidentDocument.Id.ToString())
                );

                // Start agent execution
                await agentInboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
                    ThreadId: thread.Id,
                    AgentContextId: agentContext.Id,
                    MessageId: thread.StartMessage?.Id ?? new Guid(),
                    Message: $"Please analyze and route IncidentId {incidentDocument.Id} for RCA analysis.",
                    UserId: "icm-scanner",
                    DisplayName: "ICM Scanner",
                    Timestamp: DateTime.UtcNow
                ));

                var threadUrl = $"{WebBaseUrl}/static/#/views/activities/threads/{thread.Id}";
                logger.LogInternalInformation($"[IcmScanner] Automated RCA thread created and started for incident {incidentDocument.Id}. Thread ID: {thread.Id}, URL: {threadUrl}",
                    incidentDocument.Id, thread.Id, threadUrl);

                // Start background monitoring for RCA completion
                _ = Task.Run(() => MonitorRCACompletionAsync(incidentDocument.Id.ToString(), thread.Id, threadUrl, owningTeamId));
            }
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[IcmScanner] Error executing automated RCA for incident {incidentId}", incidentDocument.Id);

            // Remove from processed incidents on error
            _processedIncidents.Remove(incidentDocument.Id.ToString());
        }
    }

    /// <summary>
    /// Gets agent contexts for a specific thread
    /// </summary>
    /// <param name="threadId">The thread ID</param>
    /// <returns>List of agent contexts</returns>
    private async Task<List<AgentContext>?> GetAgentContextsForThread(Guid threadId)
    {
        try
        {
            var query = container.GetItemLinqQueryable<AgentContextDocument>()
                .Where(doc => doc.DocumentType == "AgentContext" && doc.ThreadId == threadId.ToString())
                .ToFeedIterator();

            var results = new List<AgentContext>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                foreach (var doc in response)
                {
                    results.Add(new AgentContext(
                        Id: Guid.Parse(doc.Id),
                        ThreadId: Guid.Parse(doc.ThreadId),
                        AgentType: doc.AgentType,
                        ContextState: doc.ContextState,
                        WaitInformation: doc.WaitInformation,
                        ApprovalInformation: doc.ApprovalInformation,
                        CurrentAgent: doc.CurrentAgent,
                        AllowedTools: doc.AllowedTools
                    ));
                }
            }
            return results;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[IcmScanner] Error getting agent contexts for thread {threadId}", threadId);
            return null;
        }
    }

    /// <summary>
    /// If is newly created or newly transferred, agent need to handle it.
    /// </summary>
    /// <param name="incident"></param>
    /// <returns></returns>
    private async Task<bool> isIncidentNeedToHandle(ICMIncident incident)
    {
        //check if is newly created
        if (incident.CreatedDate > DateTime.UtcNow.AddMinutes(-5))
        {
            logger.LogInternalInformation("[IcmScanner] Incident {incidentId} is newly created.", incident.Id);
            return true;
        }
        //check if is newly transferred
        var discussionEntries = await icmApiClient.GetIncidentDiscussionEntriesAsync(incident.Id.ToString());
        var hasTransferDiscussionEntry = discussionEntries.Any(entry => entry.Date > DateTime.UtcNow.AddMinutes(-5) && entry.Text.StartsWith("<div>Transferred from", StringComparison.OrdinalIgnoreCase));
        if (hasTransferDiscussionEntry)
        {
            logger.LogInternalInformation("[IcmScanner] Incident {incidentId} is newly transferred.", incident.Id);
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------
    // Local test queue processing
    // ------------------------------------------------------------
    private async Task ProcessTestQueueAsync(CancellationToken cancellationToken)
    {
        var batch = IcmScannerTestQueueHelper.Drain();
        if (batch.Count == 0)
        {
            return;
        }

        logger.LogInternalInformation("[IcmScanner] Processing {count} test queue incident(s).", batch.Count);
        foreach (var item in batch)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var icmIncident = await icmApiClient.GetIncidentAsync(item.IncidentId);
                if (icmIncident == null)
                {
                    logger.LogInternalWarning("[IcmScanner] Test queue incident {incidentId} not found via ICM API.", item.IncidentId);
                    continue;
                }
                var incidentDoc = await GetDocumentAsync<IcmIncidentDocument>(item.IncidentId, item.IncidentId);
                incidentDoc = await UpsertIncidentDocumentIfNeededAsync(incidentDoc, icmIncident, null, cancellationToken);

                // Use automated RCA path only when feature enabled; otherwise fall back to standard notification.
                if (IsAutomatedRCAEnabled && item.ForceTeamSpecific && !string.IsNullOrWhiteSpace(item.OwningTeamId))
                {
                    // Minimal synthetic filter payload for team-specific processing.
                    var payload = new IcmIncidentFilterDocumentPayload
                    {
                        OwningTeamId = item.OwningTeamId,
                        TitleContains = string.Empty,
                        IncidentType = string.Empty
                    };
                    await ProcessTeamSpecificIncident(incidentDoc, payload, forceRun: true);
                }
                else
                {
                    await NotifyUserAsync(incidentDoc, new List<string>(), null);
                }
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "[IcmScanner] Error handling test queue incident {incidentId}", item.IncidentId);
            }
        }
    }

    private async Task<DateTime> UpdateLastScanTimeDocAsync(DateTime lastScanTime, IncidentManagementType type)
    {
        try
        {
            var patchOperationList = new List<PatchOperation>()
            {
                PatchOperation.Add($"/lastScanTime", lastScanTime)
            };

            var lastScanTimeKey = LastScanTimeDoc.GetLastScanTimeKey(type);
            var doc = await container.PatchItemAsync<LastScanTimeDoc>(
                lastScanTimeKey,
                new PartitionKey(lastScanTimeKey),
                patchOperationList
            );
            return doc.Resource.LastScanTime;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            var lastScanTimeKey = LastScanTimeDoc.GetLastScanTimeKey(type);
            var lastScanTimeDoc = new LastScanTimeDoc
            {
                Id = lastScanTimeKey,
                DocumentType = lastScanTimeKey,
                PartitionKey = lastScanTimeKey,
                LastScanTime = lastScanTime
            };
            var doc = await container.CreateItemAsync(lastScanTimeDoc, new PartitionKey(lastScanTimeKey));
            return doc.Resource.LastScanTime;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[IcmScanner] Error updating LastScanTime for {incidentType} scanner", type);
            return DateTime.UtcNow;
        }
    }
}

public class NullableIncidentScanner : IIncidentScanner
{
    public Task ScanAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(0);
    }
}
