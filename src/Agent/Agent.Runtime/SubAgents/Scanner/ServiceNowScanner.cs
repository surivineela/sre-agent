// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Models.ICM;
using Agent.Core.Models.ServiceNow;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Agent.Logging;
using System.Text.Json;

namespace Agent.Runtime.SubAgents.Scanner;

public class ServiceNowScanner(ILogger<ServiceNowScanner> logger,
    IServiceNowAPIClient serviceNowApiClient,
    CosmosClient cosmosClient,
    CosmosDBSettings cosmosDbSettings,
    IIncidentHandlingService<ServiceNowIncidentFilterDocumentPayload> incidentHandlingService,
    IIncidentManagementService<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocumentPayload> incidentManagementService,
    IIncidentFilterManagementService<ServiceNowIncidentFilterDocument, ServiceNowIncidentFilterDocumentPayload> incidentFilterManagementService,
    IAgentInboundCommunicationService agentInboundCommunicationService,
    IIncidentAnalysisService<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocumentPayload> incidentAnalysisService) : IIncidentScanner
{
    private readonly Container container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
    private const uint PageSize = 20;
    private readonly static TimeSpan ScanInterval = TimeSpan.FromMinutes(1);
    private DateTime lastScanTime;
    private readonly static int maxOffset = 200;

    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        var lastScanTimeDoc = await GetDocumentAsync<LastScanTimeDoc>(LastScanTimeDoc.LastScanTimeKey, LastScanTimeDoc.LastScanTimeKey);
        lastScanTime = lastScanTimeDoc != null ? lastScanTimeDoc.LastScanTime : DateTime.UtcNow.AddDays(-30); // Default to 30 days ago if not found

        while (!cancellationToken.IsCancellationRequested)
        {
            var filters = await incidentFilterManagementService.ListIncidentFilters();
            if (filters is null || filters.Count == 0)
            {
                logger.LogInternalInformation("No incident filters found, skipping ServiceNow scanner.");
            }
            else
            {
                logger.LogInternalInformation("Found {filterCount} incident filters, starting ServiceNow scanner.", filters.Count);
                bool isSuccess = await ScanAllIncidentsAsync(cancellationToken, filters);
                if (isSuccess)
                {
                    lastScanTime = await UpdateLastScanTimeDocAsync(DateTime.UtcNow);
                    logger.LogInternalInformation($"ServiceNow scanner completed successfully, last scan time is updated to {lastScanTime}");
                }
                else
                {
                    logger.LogInternalWarning("ServiceNow scanner encountered issues during scanning, last scan time will not be updated.");
                }

            }
            await Task.Delay(ScanInterval, cancellationToken);
        }
    }

    private async Task<bool> ScanAllIncidentsAsync(CancellationToken cancellationToken, List<ServiceNowIncidentFilterDocument> filters)
    {
        // set to true if at least one filterDocument has scanned successfully
        bool isSuccess = false;

        foreach (var filter in filters)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInternalInformation("Cancellation requested, stopping the ServiceNow scanner.");
                return isSuccess;
            }

            if (filter is ServiceNowIncidentFilterDocument filterDocument && filterDocument.DocumentType == "IncidentFilterServiceNow")
            {
                try
                {
                    isSuccess = await ScanIncidentsForFilter(filterDocument, cancellationToken) || isSuccess;
                }
                catch (Exception ex)
                {
                    logger.LogInternalError(ex, "Error scanning incidents for filter {filterId}", filterDocument.Id);
                }
            }
        }

        return isSuccess;
    }

    private async Task<bool> ScanIncidentsForFilter(ServiceNowIncidentFilterDocument filterDocument, CancellationToken cancellationToken)
    {
        logger.LogInternalInformation("Scanning incidents for filter {filterId}", filterDocument.Id);
        uint page = 0;
        bool isSuccess = false;

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInternalInformation("Cancellation requested, stopping the ServiceNow scanner.");
                return isSuccess;
            }

            uint offset = page * PageSize;

            if (offset > maxOffset)
            {
                logger.LogInternalInformation("Stop scanning ServiceNow incidents over {offset}", offset);
                return isSuccess;
            }

            try
            {
                logger.LogInternalInformation("Scanning ServiceNow incidents, page {page}, lastScanTime {lastScanTime}", page, lastScanTime);
                var incidents = await serviceNowApiClient.GetIncidentsAsync(PageSize, offset, lastScanTime, filterDocument.ImpactedService, filterDocument.TitleContains);
                isSuccess = true;
                if (incidents is null || incidents.Count == 0)
                {
                    logger.LogInternalInformation("No incidents found for filter {filterId}", filterDocument.Id);
                    return isSuccess;
                }

                foreach (var incident in incidents)
                {
                    // Use Number as document ID instead of IncidentId (sys_id)
                    var incidentDocument = await GetDocumentAsync<ServiceNowIncidentDocument>(incident.Number, incident.Number);
                    incidentDocument = await UpsertIncidentDocumentIfNeededAsync(incidentDocument, incident);
                    await NotifyUserAsync(incidentDocument, new List<string>(), filterDocument);
                }

                //Between each page, wait for 1 minute
                await Task.Delay(ScanInterval);
            }
            catch (Exception ex)
            {
                isSuccess = false;
                logger.LogInternalError(ex, "Error scanning ServiceNow incidents");
            }
            page++;
        }
    }

    private async Task<T?> GetDocumentAsync<T>(string id, string partitionKey) where T : ICosmosDocument
    {
        try
        {
            ItemResponse<T> response = await container.ReadItemAsync<T>(
                id,
                new PartitionKey(partitionKey)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }
    }

    private async Task<ServiceNowIncidentDocument> UpsertIncidentDocumentIfNeededAsync(ServiceNowIncidentDocument? incidentDocument, ServiceNowIncident incident, CancellationToken cancellationToken = default)
    {
        try
        {
            if (incidentDocument is null)
            {
                logger.LogInternalInformation("Creating new incident document for ServiceNow incident {incidentId} with number {incidentNumber}", incident.IncidentId, incident.Number);

                // Create new document with Number as ID
                incidentDocument = new ServiceNowIncidentDocument(incident)
                {
                    // ServiceNowIncidentDocument constructor will now use Number instead of IncidentId as ID
                };

                incidentDocument = await container.CreateItemAsync(incidentDocument, new PartitionKey(incidentDocument.PartitionKey), cancellationToken: cancellationToken);

                logger.LogInternalInformation("Created new incident document for ServiceNow incident {incidentNumber}", incident.Number);
            }
            else if (incidentDocument.Id == incident.Number)
            {
                var updatedDoc = new ServiceNowIncidentDocument(incident);

                // if App Insights doesn't have the latest status change to Resolved, ingest into App Insights **** We don't care about Closed for ServiceNow Incident Analysis
                if ((updatedDoc.Status.ToLower() == "resolved" || updatedDoc.Status.ToLower() == "closed") &&
                   (incidentDocument.Tags != null && incidentDocument.Tags.Contains("SREAgent_Resolved")))
                {
                    updatedDoc.Tags = incidentDocument.Tags;

                    if (string.IsNullOrWhiteSpace(incidentDocument.RootCause) || string.IsNullOrWhiteSpace(incidentDocument.GeneralSummary))
                    {
                        try
                        {
                            updatedDoc = await incidentAnalysisService.AnalyzeIncident(updatedDoc, incident);
                        }
                        catch (Exception ex)
                        {
                            logger.LogInternalError($"[ServiceNowScanner] Error generating AI-generated insights for incident; {ex.Message}");
                        }
                    }
                }

                if (updatedDoc.UpdatedAt > incidentDocument.UpdatedAt)
                {
                    try
                    {
                        await incidentAnalysisService.Ingest(updatedDoc);
                    }
                    catch (Exception ex)
                    {
                        logger.LogInternalError(ex, "[ServiceNowScanner] Failed to ingest incident data into App Insights");
                    }
                }

                logger.LogInternalInformation("Upserting existing incident document for ServiceNow incident {incidentNumber}", incident.Number);
                incidentDocument = await container.UpsertItemAsync(updatedDoc, new PartitionKey(updatedDoc.PartitionKey), cancellationToken: cancellationToken);
            }

            if (incidentDocument == null)
            {
                throw new Exception($"Failed to upsert incident document for incident {incident.IncidentId} with number {incident.Number} because incidentDocument was null");
            }

            return incidentDocument;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error upserting incident document for ServiceNow incident {incidentId} with number {incidentNumber}", incident.IncidentId, incident.Number);
            throw;
        }
    }

    private async Task<DateTime> UpdateLastScanTimeDocAsync(DateTime lastScanTime)
    {
        try
        {
            var patchOperationList = new List<PatchOperation>()
            {
                PatchOperation.Add($"/lastScanTime", lastScanTime)
            };

            var doc = await container.PatchItemAsync<LastScanTimeDoc>(
                LastScanTimeDoc.LastScanTimeKey,
                new PartitionKey(LastScanTimeDoc.LastScanTimeKey),
                patchOperationList
            );

            return doc.Resource.LastScanTime;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            var lastScanTimeDoc = new LastScanTimeDoc
            {
                LastScanTime = DateTime.UtcNow
            };

            var doc = await container.CreateItemAsync(lastScanTimeDoc, new PartitionKey(lastScanTimeDoc.PartitionKey));
            return doc.Resource.LastScanTime;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error updating LastScanTime for ServiceNowScanner");
            return DateTime.UtcNow;
        }
    }

    private async Task NotifyUserAsync(ServiceNowIncidentDocument incidentDocument, List<string> relatedResourceIds, ServiceNowIncidentFilterDocument filterDocument)
    {
        try
        {
            if (incidentDocument is null)
            {
                logger.LogInternalWarning("Incident document is null, skipping notification.");
                return;
            }

            // ServiceNow status values: 6=resolved, 7=closed, 8=cancelled
            if (incidentDocument.Status.Equals("6") || incidentDocument.Status.Equals("7") || incidentDocument.Status.Equals("8"))
            {
                logger.LogInternalInformation("Incident {incidentNumber} is resolved/closed/cancelled (status: {status}), updating thread status if exists.", incidentDocument.Id, incidentDocument.Status);

                // Update thread status if exists
                try
                {
                    var existingThreadDocument = await GetIncidentThread(incidentDocument.Id);
                    if (existingThreadDocument is not null)
                    {
                        if (existingThreadDocument.IncidentStatus != "resolved")
                        {
                            // Log agent action event for incident resolution only when status changes
                            try
                            {
                                // Determine if the incident was resolved by Agent or User based on tags
                                var resolvedBy = "User"; // Default to User
                                if (incidentDocument.Tags?.Any(tag => tag.Equals("SREAgent_Resolved", StringComparison.OrdinalIgnoreCase)) == true)
                                {
                                    resolvedBy = "Agent";
                                }

                                var resolveActionData = new IncidentResolveActionData
                                {
                                    IncidentSource = "ServiceNow",
                                    IncidentId = incidentDocument.Id,
                                    Status = GetServiceNowStatusText(incidentDocument.Status),
                                    ResolvedBy = resolvedBy,
                                };

                                logger.LogAgentAction(
                                    action: AgentActionEvents.ResolveIncident,
                                    parameter: JsonSerializer.Serialize(resolveActionData),
                                    status: AgentActionStatus.Success,
                                    duration: 0,
                                    threadId: existingThreadDocument.Id);
                            }
                            catch (Exception ex)
                            {
                                logger.LogInternalError(ex, "Error logging agent action for ServiceNow incident resolution {incidentNumber}", incidentDocument.Id);
                            }

                            existingThreadDocument.IncidentStatus = "resolved";
                            logger.LogInternalInformation("Updating thread status to resolved for ServiceNow incident {incidentNumber}", incidentDocument.Id);
                            await container.UpsertItemAsync(existingThreadDocument, new PartitionKey(existingThreadDocument.Id));
                            logger.LogInternalInformation("Updated thread status to resolved for ServiceNow incident {incidentNumber}", incidentDocument.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogInternalError(ex, "Error updating thread status for ServiceNow incident {incidentNumber}", incidentDocument.Id);
                }
                return;
            }

            var threadDocument = await GetIncidentThread(incidentDocument.Id);
            if (threadDocument is null)
            {
                logger.LogInternalInformation("Thread doesn't exist for incident {incidentNumber} by filter {filterId}, creating new incident thread", incidentDocument.Id, filterDocument.Id);
                var response = await incidentHandlingService.HandleIncidentAsync(new IncidentHandlingRequestModel<ServiceNowIncidentFilterDocumentPayload>()
                {
                    IncidentId = incidentDocument.Id,
                    Title = incidentDocument.Title,
                    Description = incidentDocument.Description,
                    Severity = incidentDocument.Priority,
                    Source = "ServiceNow"
                });
            }
            else
            {
                logger.LogInternalInformation("Thread already exists for incident {incidentNumber}, checking for updates", incidentDocument.Id);
                var existingIncidentDocument = await incidentManagementService.GetIncidentDetails(incidentDocument.Id);
                var existingDiscussionEntries = existingIncidentDocument != null ? existingIncidentDocument.DiscussionEntries : new List<DiscussionEntry>();
                // Keep using IncidentId (sys_id) for API calls to get discussion entries
                var latestDiscussionEntries = await serviceNowApiClient.GetIncidentDiscussionEntriesAsync(incidentDocument.IncidentSystemId);

                var newNotes = latestDiscussionEntries
                        .Skip(existingDiscussionEntries.Count)
                        .Select(entry => new IncidentDiscussion(entry.IncidentId, entry.Text, entry.ChangedBy, entry.ChangedBy, entry.Date))
                        .ToList();

                if (newNotes.Count > 0)
                {
                    logger.LogInternalInformation("Found {newNotesCount} new notes for incident {incidentNumber}", newNotes.Count, incidentDocument.Id);
                    await agentInboundCommunicationService.AddNewDiscussionsToIncidentThread(Guid.Parse(threadDocument.Id), newNotes);
                    logger.LogInternalInformation("Added {newNotesCount} new notes to incident thread {threadId}", newNotes.Count, threadDocument.Id);
                }
                else
                {
                    logger.LogInternalInformation("No new notes found for incident {incidentNumber}", incidentDocument.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error notifying user about incident {incidentNumber}", incidentDocument.Id);
        }
    }

    private async Task<ThreadDocument?> GetIncidentThread(string incidentNumber)
    {
        var threads = container.GetItemLinqQueryable<ThreadDocument>()
            .Where(doc => doc.DocumentType == "Thread" && doc.Source == ThreadSource.Incident)
            .Where(doc => (doc.IncidentSource != null && doc.IncidentSource.IncidentType == Agent.Core.Models.Api.v1.IncidentType.ServiceNow && doc.IncidentSource.IncidentId == incidentNumber) || (doc.IncidentId == incidentNumber))
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
                logger.LogInternalWarning("Multiple threads({threadIds}) found for incident {incidentNumber}, returning the first one.", string.Join(',', response.Select(t => t.Id)), incidentNumber);
                return response.FirstOrDefault();
            }
        }
        return null;
    }

    /// <summary>
    /// Converts ServiceNow status codes to readable text
    /// </summary>
    /// <param name="statusCode">ServiceNow status code</param>
    /// <returns>Readable status text</returns>
    private static string GetServiceNowStatusText(string statusCode)
    {
        return statusCode switch
        {
            "6" => "Resolved",
            "7" => "Closed",
            "8" => "Cancelled",
            _ => $"Status_{statusCode}"
        };
    }
}
