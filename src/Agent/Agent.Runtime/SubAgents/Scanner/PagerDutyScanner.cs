// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Graph.Interfaces;
using Agent.Logging;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using PagerDutyIncident = Agent.Graph.Interfaces.PagerDutyIncident;

namespace Agent.Runtime.SubAgents.Scanner;

public class PagerDutyScanner(ILogger<PagerDutyScanner> logger,
                              IPagerDutyService pagerDutyService,
                              CosmosClient cosmosClient,
                              CosmosDBSettings cosmosDbSettings,
                              IChatClientProvider chatClientProvider,
                              IGraphDatabaseClient graphDbClient,
                              IncidentManagementSettings incidentManagementSettings,
                              IIncidentHandlingService<PagerDutyIncidentFilterDocumentPayload> incidentHandlingService,
                              IIncidentFilterManagementService<PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload> incidentFilterManagementService,
                              IIncidentAnalysisService<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload, PagerDutyIncident> incidentAnalysisService,
                              IAgentInboundCommunicationService agentInboundCommunicationService) : IIncidentScanner
{
    private readonly Container container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
    private const uint PageSize = 10;
    private readonly static TimeSpan ScanInterval = TimeSpan.FromMinutes(1);
    private DateTime lastScanTime;
    private DateTime latestIncidentCreatedAtUtc;
    private bool isScanSucceeded = true;

    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        if (incidentManagementSettings is null || incidentManagementSettings.Type != IncidentManagementType.PagerDuty)
        {
            logger.LogInternalInformation("PagerDuty is not configured. Skipping scanning.");
            return;
        }

        if (string.IsNullOrEmpty(incidentManagementSettings.ConnectionKey))
        {
            logger.LogInternalWarning("PagerDuty API key is not configured. Skipping scanning.");
            return;
        }

        var lastScanTimeKey = LastScanTimeDoc.GetLastScanTimeKey(IncidentManagementType.PagerDuty);
        var lastScanTimeDoc = await GetDocumentAsync<LastScanTimeDoc>(lastScanTimeKey, lastScanTimeKey);
        lastScanTime = lastScanTimeDoc != null ? lastScanTimeDoc.LastScanTime : DateTime.UtcNow.AddDays(-30); // Default to 30 days ago if not found
        latestIncidentCreatedAtUtc = lastScanTime;
        while (!cancellationToken.IsCancellationRequested)
        {
            latestIncidentCreatedAtUtc = lastScanTime;
            var filters = await incidentFilterManagementService.ListIncidentFilters(false);
            if (filters is null || filters.Count == 0)
            {
                logger.LogInternalInformation("No incident filters found, skipping PagerDuty scanner.");
            }
            else
            {
                logger.LogInternalInformation("Found {filterCount} incident filters, starting PagerDuty scanner.", filters.Count);
                await ScanAllIncidentsAsync(filters, cancellationToken);
                if (isScanSucceeded)
                {
                    lastScanTime = await UpdateLastScanTimeDocAsync(latestIncidentCreatedAtUtc, IncidentManagementType.PagerDuty);
                    logger.LogInternalInformation($"PagerDuty scanner completed successfully, last scanned created_at is {lastScanTime:O}");
                }
                else
                {
                    logger.LogInternalWarning("PagerDuty scanner encountered issues during scanning, last scan time will not be updated.");
                }
            }
            await Task.Delay(ScanInterval, cancellationToken);
        }
    }

    private async Task ScanAllIncidentsAsync(List<PagerDutyIncidentFilterDocument> filters, CancellationToken cancellationToken)
    {
        foreach (var filter in filters)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInternalInformation("Cancellation requested, stopping the PagerDuty scanner.");
                return;
            }

            if (filter is PagerDutyIncidentFilterDocument filterDocument && filterDocument.DocumentType == "IncidentFilterPagerDuty")
            {
                try
                {
                    await ScanIncidentsForFilter(filterDocument, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogInternalError(ex, "Error scanning incidents for filter {filterId}", filterDocument.Id);
                }
            }
        }
    }

    private async Task ScanIncidentsForFilter(PagerDutyIncidentFilterDocument filterDocument, CancellationToken cancellationToken)
    {
        uint page = 0;
        isScanSucceeded = true;

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInternalInformation("Cancellation requested, stopping the scanner.");
                isScanSucceeded = false;
                return;
            }
            var offset = page * PageSize;
            try
            {
                logger.LogInternalInformation("Scanning PagerDuty incidents, page {page}", page);
                var response = await pagerDutyService.GetIncidentsAsync(PageSize, offset, lastScanTime, filterDocument.ImpactedService,
                    filterDocument.Priority, filterDocument.TitleContains);
                if (response is null || !response.Any())
                {
                    logger.LogInternalInformation("No more incidents to process, stopping the scanner.");
                    return;
                }

                foreach (var incident in response)
                {
                    var incidentCreatedAtUtc = EnsureUtc(incident.CreatedAt);
                    if (incidentCreatedAtUtc > latestIncidentCreatedAtUtc)
                    {
                        latestIncidentCreatedAtUtc = incidentCreatedAtUtc;
                    }
                    var incidentDocument = await GetDocumentAsync<PagerDutyIncidentDocument>(incident.IncidentId, incident.IncidentId);
                    var existingLastModifiedTime = incidentDocument != null ? incidentDocument.UpdatedAt : DateTime.MinValue;
                    incidentDocument = await UpsertIncidentDocumentIfNeededAsync(incidentDocument, incident, filterDocument, cancellationToken);
                    var relatedResourceIds = await UpdateResourceGraph(incidentDocument, incident);

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
                            logger.LogInternalError(ex, "[PagerDutyScanner] Failed to ingest incident data into App Insights");
                        }
                    }

                    await NotifyUserAsync(incidentDocument, filterDocument, relatedResourceIds);
                }
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error scanning PagerDuty incidents");
                isScanSucceeded = false;
                if (ex.Message.Contains("Unauthorized"))
                {
                    return;
                }
            }

            page++;
        }
    }

    private async Task NotifyUserAsync(PagerDutyIncidentDocument incidentDocument, PagerDutyIncidentFilterDocument filterDocument, List<string> relatedResourceIds)
    {
        try
        {
            if (incidentDocument is null)
            {
                logger.LogInternalWarning("Incident document is null, skipping notification.");
                return;
            }

            if (incidentDocument.Status == "resolved")
            {
                logger.LogInternalInformation("Incident {incidentId} is resolved, skipping notification.", incidentDocument.Id);
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
                                    IncidentSource = "PagerDuty",
                                    IncidentId = incidentDocument.Id,
                                    Status = incidentDocument.Status,
                                    ResolvedBy = resolvedBy
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
                                logger.LogInternalError(ex, "Error logging agent action for PagerDuty incident resolution {incidentId}", incidentDocument.Id);
                            }

                            existingThreadDocument.IncidentStatus = "resolved";
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
                                logger.LogInternalInformation("Title or priority changed for PagerDuty incident {incidentId}", incidentDocument.Id);
                                logger.LogInternalInformation("Updating incident title or priority on PagerDuty incident thread {threadId}", existingThreadDocument.Id);
                            }
                            if (needsUpsertForResolvedStatus)
                            {
                                logger.LogInternalInformation("Updating thread status to resolved for PagerDuty incident {incidentId}", incidentDocument.Id);
                            }

                            await container.UpsertItemAsync(existingThreadDocument, new PartitionKey(existingThreadDocument.Id));

                            if (needsUpsertForDetailsChange)
                            {
                                logger.LogInternalInformation("Updated incident title or priority on PagerDuty incident thread {threadId}", existingThreadDocument.Id);
                            }
                            if (needsUpsertForResolvedStatus)
                            {
                                logger.LogInternalInformation("Updated thread status to resolved for PagerDuty incident {incidentId}", incidentDocument.Id);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogInternalError(ex, "Error updating thread status for incident {incidentId}", incidentDocument.Id);
                }
                return;
            }

            var threadDocument = await GetIncidentThread(incidentDocument.Id);
            if (threadDocument is null)
            {
                logger.LogInternalInformation("Thread doesn't exist for incident {incidentId}, skipping notification", incidentDocument.Id);
                var response = await incidentHandlingService.HandleIncidentAsync(new IncidentHandlingRequestModelWithFilterOnly<PagerDutyIncidentFilterDocumentPayload>()
                {
                    IncidentId = incidentDocument.Id,
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
                logger.LogInternalInformation("Thread already exists for incident {incidentId}, checking whether it needs to be updated", incidentDocument.Id);

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
                        logger.LogInternalInformation("Title or priority changed for PagerDuty incident {incidentId}", incidentDocument.Id);
                        logger.LogInternalInformation("Updating incident title or priority on PagerDuty incident thread {threadId}", threadDocument.Id);
                        await container.UpsertItemAsync(threadDocument, new PartitionKey(threadDocument.Id));
                        logger.LogInternalInformation("Updated incident title or priority on PagerDuty incident thread {threadId}", threadDocument.Id);
                    }
                }

                // todo
                var iterator = container.GetItemLinqQueryable<MessageDocument>()
                    .Where(doc => doc.DocumentType == "Message" && doc.ThreadId == threadDocument.Id)
                    .Where(doc => doc.IncidentDiscussionId != null)
                    .Select(doc => doc.IncidentDiscussionId!)
                    .ToFeedIterator();
                // .ToHashSet();

                var existingNotesIds = new HashSet<string>();
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    foreach (var item in response)
                    {
                        existingNotesIds.Add(item);
                    }
                }
                logger.LogInternalInformation("Found {existingNotesCount} existing notes for incident {incidentId}", existingNotesIds.Count, incidentDocument.Id);

                var newNotes = incidentDocument.Notes
                    .Where(note => !existingNotesIds.Contains(note.Id))
                    .OrderBy(note => note.CreatedAt)
                    .Select(note => new IncidentDiscussion(note.Id, note.Content, note.CreatedBy?.Id ?? "Unknown", note.CreatedBy?.Name ?? "Unknown", note.CreatedAt))
                    .ToList();

                if (newNotes.Count > 0)
                {
                    logger.LogInternalInformation("Found {newNotesCount} new notes for incident {incidentId}", newNotes.Count, incidentDocument.Id);
                    await agentInboundCommunicationService.AddNewDiscussionsToIncidentThread(Guid.Parse(threadDocument.Id), newNotes);
                    logger.LogInternalInformation("Added {newNotesCount} new notes to incident thread {threadId}", newNotes.Count, threadDocument.Id);
                }
                else
                {
                    logger.LogInternalInformation("No new notes found for incident {incidentId}", incidentDocument.Id);
                }

            }

        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error notifying user about incident {incidentId}", incidentDocument.Id);
        }

    }

    private async Task<ThreadDocument?> GetIncidentThread(string incidentId)
    {
        var threads = container.GetItemLinqQueryable<ThreadDocument>()
            .Where(doc => doc.DocumentType == "Thread" && doc.Source == ThreadSource.Incident)
            .Where(doc => doc.IncidentSource != null && doc.IncidentSource.IncidentType == IncidentType.PagerDuty && doc.IncidentSource.IncidentId == incidentId || doc.IncidentId == incidentId)
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
                logger.LogInternalWarning("Multiple threads({threadIds}) found for incident {incidentId}, returning the first one.", string.Join(',', response.Select(t => t.Id)), incidentId);
                return response.FirstOrDefault();
            }
        }
        return null;
    }

    private async Task<PagerDutyIncidentDocument> UpsertIncidentDocumentIfNeededAsync(PagerDutyIncidentDocument? incidentDocument, Graph.Interfaces.PagerDutyIncident incident, PagerDutyIncidentFilterDocument filterDocument, CancellationToken cancellationToken = default)
    {
        try
        {
            var latestDetails = await pagerDutyService.GetLatestIncidentDetails(incident.IncidentId);
            var needsUpsert = false;
            // TODO: check latest title
            if (incidentDocument is null)
            {
                needsUpsert = true;
                logger.LogInternalInformation("Creating new incident document by id {incidentId}", incident.IncidentId);
                incidentDocument = new PagerDutyIncidentDocument(
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
                    // Well done PagerDuty. Took me hours to figure out where to find the real description.
                    Description = incident.FirstTriggerLogEntry.Channel?.Details.ToString() ?? incident.Description,
                    UpdatedAt = incident.UpdatedAt
                };

                if (latestDetails is not null)
                {
                    incidentDocument.Notes = latestDetails.Notes;

                    if (!string.IsNullOrEmpty(latestDetails.LatestDescription) && incidentDocument.Description != latestDetails.LatestDescription)
                    {
                        incidentDocument.Description = latestDetails.LatestDescription;
                    }

                    if (!string.IsNullOrEmpty(latestDetails.LatestTitle) && incidentDocument.Title != latestDetails.LatestTitle)
                    {
                        incidentDocument.Title = latestDetails.LatestTitle;
                    }
                }

                // var titleEmbedding = await embeddingGenerator.GenerateEmbeddingAsync(incident.Title, cancellationToken: cancellationToken);
                // TODO: try to avoid this copy
                // incidentDocument.TitleVector = titleEmbedding.Vector.ToArray();
                // var descriptionEmbedding = await embeddingGenerator.GenerateEmbeddingAsync(incident.Description, cancellationToken: cancellationToken);
                // incidentDocument.DescriptionVector = descriptionEmbedding.Vector.ToArray();

                _ = await container.CreateItemAsync(incidentDocument, new PartitionKey(incidentDocument.PartitionKey), cancellationToken: cancellationToken);
                logger.LogInternalInformation("Created new incident document for ServiceNow incident {incidentNumber}", incident.IncidentId);
            }
            else
            {
                logger.LogInternalInformation("Updating existing incident document by id {incidentId}", incident.IncidentId);
                if (latestDetails is not null)
                {
                    if (!string.IsNullOrEmpty(latestDetails.LatestDescription) && incidentDocument.Description != latestDetails.LatestDescription)
                    {
                        incidentDocument.Description = latestDetails.LatestDescription;
                        needsUpsert = true;
                    }

                    if (!string.IsNullOrEmpty(latestDetails.LatestTitle) && incidentDocument.Title != latestDetails.LatestTitle)
                    {
                        incidentDocument.Title = latestDetails.LatestTitle;
                        needsUpsert = true;
                    }
                    if (incidentDocument.Notes.Count < latestDetails.Notes.Count)
                    {
                        incidentDocument.Notes = latestDetails.Notes;
                        needsUpsert = true;
                    }
                    if (incidentDocument.Status != incident.Status)
                    {
                        incidentDocument.Status = incident.Status;
                        // Set ResolvedAt when status changes to resolved
                        if (incident.Status.Equals("resolved", StringComparison.OrdinalIgnoreCase))
                        {
                            incidentDocument.ResolvedAt = incident.UpdatedAt;
                        }
                        needsUpsert = true;
                    }
                    if (incident.Priority != null && incidentDocument.Priority != incident.Priority.Summary)
                    {
                        incidentDocument.Priority = incident.Priority.Summary;
                        needsUpsert = true;
                    }
                    if (incident.ImpactedService != null && incidentDocument.ImpactedServiceId != incident.ImpactedService.Id)
                    {
                        incidentDocument.ImpactedServiceId = incident.ImpactedService.Id;
                        needsUpsert = true;
                    }
                    if (incident.ImpactedService != null && incidentDocument.ImpactedServiceName != incident.ImpactedService.Summary)
                    {
                        incidentDocument.ImpactedServiceName = incident.ImpactedService.Summary;
                        needsUpsert = true;
                    }
                    if (incident.IncidentType != null && incidentDocument.IncidentType != incident.IncidentType.Name)
                    {
                        incidentDocument.IncidentType = incident.IncidentType.Name;
                        needsUpsert = true;
                    }

                    if (incidentDocument.UpdatedAt < incident.UpdatedAt)
                    {
                        incidentDocument.UpdatedAt = incident.UpdatedAt;
                        needsUpsert = true;
                    }

                    // Once incident is mitigated or resolved, do AI analysis
                    if ((incidentDocument.Status.ToLower() == "resolved" || incidentDocument.Status.ToLower() == "closed") &&
                        (string.IsNullOrWhiteSpace(incidentDocument.AIRootCause) || string.IsNullOrWhiteSpace(incidentDocument.RootCauseDescription) || string.IsNullOrWhiteSpace(incidentDocument.GeneralSummary)))
                    {
                        try
                        {
                            incidentDocument = await incidentAnalysisService.AnalyzeIncident(incidentDocument, incident, filterDocument);
                            needsUpsert = true;
                        }
                        catch (Exception ex)
                        {
                            logger.LogInternalError($"[PagerDutyScanner] Error generating AI-generated insights for incident; {ex.Message}");
                        }
                    }
                }

                var newNotes = incidentDocument.Notes
                    .Where(note => note.CreatedAt > lastScanTime.AddMinutes(-5))
                    .OrderBy(note => note.CreatedAt)
                    .Select(note => new IncidentDiscussion(note.Id, note.Content, note.CreatedBy?.Id ?? "Unknown", note.CreatedBy?.Name ?? "Unknown", note.CreatedAt))
                    .ToList();

                if (newNotes.Count > 0)
                {
                    // Check if agent assisted with incident based on new notes
                    if (!incidentDocument.IsAssistedByAgent)
                    {
                        var agentAssisted = await incidentAnalysisService.DetermineAgentAssistanceFromNotes(incidentDocument, newNotes);
                        if (agentAssisted)
                        {
                            logger.LogInternalInformation("[PagerDutyScanner] Detected agent assistance for incident {incidentId}, updating document", incidentDocument.Id);
                            incidentDocument.IsAssistedByAgent = true;
                            needsUpsert = true;
                        }
                    }
                }
                // var descriptionEmbedding = await embeddingGenerator.GenerateEmbeddingAsync(incident.Description, cancellationToken: cancellationToken);
                // TODO: try to avoid this copy
                // incidentDocument.DescriptionVector = descriptionEmbedding.Vector.ToArray();
            }

            // todo: maybe use patch instead of upsert for updating existing incidents.
            if (needsUpsert)
            {
                await container.UpsertItemAsync(incidentDocument, new PartitionKey(incident.IncidentId), cancellationToken: cancellationToken);
                logger.LogInternalInformation("Upserted incident document for PagerDuty incident {incidentId}", incident.IncidentId);
            }
            else
            {
                logger.LogInternalInformation("No changes detected for PagerDuty incident {incidentId}", incident.IncidentId);
            }
            return incidentDocument;

        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error upserting incident document for PagerDuty incident {incidentId}", incident.IncidentId);
            throw;
        }
    }

    private async Task<List<string>> UpdateResourceGraph(PagerDutyIncidentDocument incidentDocument, Graph.Interfaces.PagerDutyIncident incident)
    {
        if (incidentDocument is null)
        {
            logger.LogInternalWarning("Incident document is null, skipping resource graph update.");
            return [];
        }

        try
        {
            var incidentNode = new PagerDutyIncidentNode
            {
                IncidentId = incident.IncidentId
            };
            var result = await graphDbClient.AddOrUpdateNodeAsync(incidentNode);
            logger.LogInternalInformation("Upserted incident node for {incidentId}", incident.IncidentId);

            if (!string.IsNullOrEmpty(incidentDocument.Description))
            {
                var relatedResourceIds = await GetRelatedResourceIdsAsync(incidentDocument.Description);
                logger.LogInternalInformation("Related resource ids to incident {incidentId}: {relatedResourceIds}", incident.IncidentId, string.Join(", ", relatedResourceIds));

                foreach (var resourceId in relatedResourceIds)
                {
                    if (string.IsNullOrEmpty(resourceId))
                    {
                        logger.LogInternalWarning("Related resource id is null or empty for incident {incidentId}", incident.IncidentId);
                        continue;
                    }
                    var nodeId = await graphDbClient.GetNodeId(resourceId);
                    if (string.IsNullOrEmpty(nodeId))
                    {
                        logger.LogInternalWarning("{resourceId} related to incident {incidentId} doesn't exist in knowledge graph", resourceId, incident.IncidentId);
                        continue;
                    }
                    var edge = new RelatedToIncidentEdge
                    {
                        SourceNodeId = nodeId,
                        TargetNodeId = incidentNode.GetNodeId(),
                    };
                    await graphDbClient.AddOrUpdateEdgeAsync(edge);
                    logger.LogInternalInformation("Added RelatedToIncidentEdge from {resourceId} to {incidentId}", resourceId, incident.IncidentId);
                }
                return relatedResourceIds;
            }
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error updating resource graph for incident {incidentId}", incident.IncidentId);
        }
        return [];
    }

    private async Task<List<string>> GetRelatedResourceIdsAsync(string incidentDescription)
    {
        var systemPrompt = "You are pager duty incident and Azure resource expert. " +
            "You are given a pager duty incident description and you need to find all Azure resources id's that is related to the incident." +
            "Note that the resource id's are in the format of /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProvider}/{resourceType}/{resourceName}." +
            "The resource id may not be given directly and you need to extract necessary information and assemble them to a resource id." +
            "Return the resource id's in a json array. If you cannot find any resource id's, return an empty json array.";
        var userPrompt = new ChatMessage(ChatRole.User, $"The incident description goes below:\n{incidentDescription}");
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            userPrompt
        };

        var options = new ChatOptions
        {
            Temperature = (float)0.2,
        };

        try
        {
            var response = await chatClientProvider.GeneralPurposeModel.GetResponseAsync<List<string>>(messages, options);
            return response.Result;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error getting related resource ids from chat client");
            return [];
        }
    }

    private async Task<DateTime> UpdateLastScanTimeDocAsync(DateTime latestTimestampUtc, IncidentManagementType type)
    {
        try
        {
            var patchOperationList = new List<PatchOperation>()
            {
                PatchOperation.Add($"/lastScanTime", latestTimestampUtc)
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
                LastScanTime = latestTimestampUtc
            };

            var doc = await container.CreateItemAsync(lastScanTimeDoc, new PartitionKey(lastScanTimeKey));
            return doc.Resource.LastScanTime;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error updating LastScanTime for {incidentType} scanner", type);
            return DateTime.UtcNow;
        }
    }

    private static DateTime EnsureUtc(DateTime timestamp)
    {
        return timestamp.Kind switch
        {
            DateTimeKind.Unspecified => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
            DateTimeKind.Utc => timestamp,
            _ => timestamp.ToUniversalTime()
        };
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
    }
}
