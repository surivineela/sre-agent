using System.Net;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Models.ICM;
using Agent.Core.Services;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Configuration;

namespace Agent.Runtime.SubAgents.IcmScanner;
public class IcmScanner(ILogger<IcmScanner> logger,
    IICMAPIClient icmApiClient,
    CosmosClient cosmosClient,
    CosmosDBSettings cosmosDbSettings,
    IIncidentHandlingService incidentHandlingService,
    IIncidentManagementService<IcmIncidentDocument> incidentManagementService,
    IIncidentFilterManagementService incidentFilterManagementService,
    IAgentInboundCommunicationService agentInboundCommunicationService,
    IAgentOutboundCommunicationService agentOutboundCommunicationService,
    IICMPlugin icmPlugin,
    IncidentManagementSettings incidentManagementSettings) : IIncidentScanner
{

    private readonly Container container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
    private const uint PageSize = 50;
    private readonly static TimeSpan ScanInterval = TimeSpan.FromMinutes(1);
    private bool isScanSucceeded = true;
    private DateTime lastScanTime;
    //After offset > 5000, ICM endpoint will returning 400 bad request
    //Updating offset to 200, since now it will apply on every existing incident Filter
    private readonly static int maxOffset = 200;
    
    // Automated RCA configuration
    private bool IsAutomatedRCAEnabled => incidentManagementSettings.AutomatedRCA.Enabled;
    private string WebBaseUrl => incidentManagementSettings.AutomatedRCA.WebBaseUrl;
    private bool IsICMAPIReadOnly => incidentManagementSettings.ICMAPI.ReadOnly;
    
    // Track processed incidents to avoid duplicate processing
    private readonly HashSet<string> _processedIncidents = new();
    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        var lastScanTimeDoc = await GetDocumentAsync<LastScanTimeDoc>(LastScanTimeDoc.LastScanTimeKey, LastScanTimeDoc.LastScanTimeKey);
        lastScanTime = lastScanTimeDoc != null ? lastScanTimeDoc.LastScanTime : DateTime.UtcNow.AddDays(-30); // Default to 30 days ago if not found
        while (!cancellationToken.IsCancellationRequested)
        {
            var filters = await incidentFilterManagementService.ListIncidentFilters();
            var scanStartTime = DateTime.UtcNow;
            if (filters is null || filters.Count == 0)
            {
                logger.LogInternalInformation("[IcmScanner] No incident filters found, skipping IcM scanner.");
            } else
            {
                logger.LogInternalInformation("[IcmScanner] Found {filterCount} incident filters, starting IcM scanner.", filters.Count);
                await ScanAllIncidentsAsync(cancellationToken,filters);
                if(isScanSucceeded)
                {
                    //Once scan scceeded, update the last scan time to startTime - 50sec to give overlap between scans
                    //Since there is 30sec lag for ICM API to update incident status
                    lastScanTime = await UpdateLastScanTimeDocAsync(scanStartTime.AddSeconds(-50));
                } else
                {
                    logger.LogInternalWarning("[IcmScanner] IcM scanner failed to scan incidents, last scan time will not be updated.");
                }
            }
            await Task.Delay(ScanInterval, cancellationToken);
        }
    }
    private async Task ScanAllIncidentsAsync(CancellationToken cancellationToken, List<IncidentFilterDocument> filters)
    {

        foreach (var filter in filters)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInternalInformation("[IcmScanner] Cancellation requested, stopping the IcM scanner.");
                return;
            }
            if (filter is IncidentFilterDocument filterDocument && filterDocument.DocumentType == "IncidentFilterIcm")
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

    private async Task ScanIncidentsForFilter(IncidentFilterDocument filterDocument, CancellationToken cancellationToken)
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
            uint offset = page * PageSize;

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
                    string.IsNullOrWhiteSpace(filterDocument.IncidentType) ? null : filterDocument.IncidentType
                );
                
                if (incidents is null || incidents.Count == 0)
                {
                    logger.LogInternalInformation("[IcmScanner] No incidents found for filter: {filterId}", filterDocument.Id);
                    return;
                }
                
                foreach (var incident in incidents)
                {
                    var incidentDocument = await GetDocumentAsync<IcmIncidentDocument>(incident.IncidentId, incident.IncidentId);
                    incidentDocument = await UpsertIncidentDocumentIfNeededAsync(incidentDocument, incident);
                    
                    // Process team-specific incidents for automated RCA when OwningTeamId is set
                    if (!string.IsNullOrWhiteSpace(filterDocument.OwningTeamId) && IsAutomatedRCAEnabled)
                    {
                        await ProcessTeamSpecificIncident(incidentDocument, filterDocument);
                    }
                    else
                    {
                        // Traditional incident handling for regular filters
                        await NotifyUserAsync(incidentDocument, new List<string>());
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
    private async Task MonitorRCACompletionAsync(string incidentId, Guid threadId, string threadUrl)
    {
        try
        {
            // Monitor for up to 24 hours, checking every 30 minutes
            var maxAttempts = 48;
            var checkInterval = TimeSpan.FromMinutes(30);

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                await Task.Delay(checkInterval);

                try
                {
                    // Check agent context state
                    var agentContexts = await GetAgentContextsForThread(threadId);
                    var activeContext = agentContexts?.FirstOrDefault();

                    if (activeContext == null)
                    {
                        logger.LogInternalWarning("[IcmScanner] No agent context found for thread {threadId}, attempt {attempt}", threadId, attempt + 1);
                        continue;
                    }

                    // Check if agent has completed
                    if (activeContext.ContextState == ContextStateEnum.Completed)
                    {
                        logger.LogInternalInformation("[IcmScanner] RCA completed for incident {incidentId}", incidentId);
                        await AddTagToICMOrThreadAsync(incidentId, threadId, "AgentProcessed");
                        _processedIncidents.Remove(incidentId);
                        return;
                    }
                    else if (activeContext.ContextState == ContextStateEnum.Failed)
                    {
                        // Error state
                        var errorMessage = $"❌ **RCA Analysis Error**: The automated analysis encountered an error. Please check the [analysis thread]({threadUrl}) for details.";
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

    private async Task<IcmIncidentDocument> UpsertIncidentDocumentIfNeededAsync(IcmIncidentDocument? incidentDocument, Incident incident, CancellationToken cancellationToken = default)
    {
        try
        {
            if (incidentDocument is null)
            {
                logger.LogInternalInformation("[IcmScanner] Creating new incident document for IcM by id {incidentId}", incident.IncidentId);

                incidentDocument = new IcmIncidentDocument(incident);
                incidentDocument = await container.CreateItemAsync(incidentDocument, new PartitionKey(incidentDocument.PartitionKey), cancellationToken: cancellationToken);

                logger.LogInternalInformation("[IcmScanner] Created new incident document for IcM incident {incidentId}", incident.IncidentId);
            }
            else if (incidentDocument.Id == incident.IncidentId)
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

                //For now use UpsertItemAsync for updating IcmIncidentDocument, later can switch to PatchItemAsync if needed.
                var updatedDoc = new IcmIncidentDocument(incident)
                {
                    UpdatedAt = DateTime.UtcNow
                };
                logger.LogInternalInformation("[IcmScanner] Upserting existing incident document for IcM incident {incidentId}", incident.IncidentId);
                incidentDocument = await container.UpsertItemAsync(updatedDoc, new PartitionKey(updatedDoc.PartitionKey), cancellationToken: cancellationToken);
            }

            if (incidentDocument == null)
            {
                throw new Exception($"Failed to create or update incident document for IcM incident {incident?.IncidentId}. The incident document is null.");
            }

            return incidentDocument;

        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.RequestEntityTooLarge)
        {
            logger.LogInternalWarning("[IcmScanner] Original IcM is too large, truncate incident details");
            incidentDocument = IcmIncidentDocument.TruncateIcmIncidentDocument(incident);
            incidentDocument = await container.UpsertItemAsync(incidentDocument, new PartitionKey(incidentDocument.PartitionKey), cancellationToken: cancellationToken);
            return incidentDocument;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[IcmScanner] Error upserting incident document for IcM incident {incidentId}", incident.IncidentId);
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
            logger.LogInternalError(ex, "[IcmScanner] Error updating LastScanTime for IcmScanner");
            return DateTime.UtcNow;
        }
    }

    private async Task NotifyUserAsync(IcmIncidentDocument incidentDocument, List<string> relatedResourceIds)
    {
        try
        {
            if (incidentDocument is null)
            {
                logger.LogInternalWarning("[IcmScanner] Incident document is null, skipping notification.");
                return;
            }

            if (incidentDocument.Status.ToString().Equals("resolved", StringComparison.OrdinalIgnoreCase) || incidentDocument.Status.ToString().Equals("mitigated", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInternalInformation("[IcmScanner] Incident {incidentId} is mitigated/resolved, skipping notification.", incidentDocument.Id);
                return;
            }

            var threadDocument = await GetIncidentThread(incidentDocument.Id);
            if (threadDocument is null)
            {
                logger.LogInternalInformation("[IcmScanner] Thread doesn't exist for incident {incidentId}, creating new thread", incidentDocument.Id);
                
                // Default incident handling (manual response)
                var response = await incidentHandlingService.HandleIncidentAsync(new IncidentHandlingRequestModel()
                {
                    IncidentId = incidentDocument.Id,
                    Title = incidentDocument.Title,
                    Description = incidentDocument.Description,
                    Severity = incidentDocument.Priority
                });
            }
            else
            {
                logger.LogInternalInformation("[IcmScanner] Thread already exists for incident {incidentId}, checking whether it needs to be updated", incidentDocument.Id);
                var existingIncidentDocument = await incidentManagementService.GetIncidentDetails(incidentDocument.Id);
                var existingDiscussionEntries = existingIncidentDocument != null ? existingIncidentDocument.DiscussionEntries : new List<DiscussionEntry>();
                var latestDiscussionEntries = await icmApiClient.GetIncidentDiscussionEntriesAsync(incidentDocument.Id);

                var newNotes = latestDiscussionEntries
                        .Skip(existingDiscussionEntries.Count)
                        .Where(entry => entry.Date > lastScanTime)
                        .Select(entry => new IncidentDiscussion(entry.IncidentId, entry.Text, entry.ChangedBy, entry.ChangedBy, entry.Date))
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
            .Where(doc => (doc.IncidentSource != null && doc.IncidentSource.IncidentType == Agent.Core.Models.Api.v1.IncidentType.Icm && doc.IncidentSource.IncidentId == incidentId) || (doc.IncidentId == incidentId))
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
    private async Task ProcessTeamSpecificIncident(IcmIncidentDocument incidentDocument, IncidentFilterDocument filterDocument)
    {
        try
        {
            if (incidentDocument is null)
            {
                logger.LogInternalWarning("[IcmScanner] Incident document is null, skipping team-specific processing.");
                return;
            }

            // Skip resolved/mitigated incidents
            if (incidentDocument.Status.ToString().Equals("resolved", StringComparison.OrdinalIgnoreCase) || 
                incidentDocument.Status.ToString().Equals("mitigated", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInternalInformation("[IcmScanner] Incident {incidentId} is mitigated/resolved, skipping team-specific processing.", incidentDocument.Id);
                return;
            }

            // Check if already processed
            if (_processedIncidents.Contains(incidentDocument.Id))
            {
                logger.LogInternalInformation("[IcmScanner] Incident {incidentId} already processed, skipping.", incidentDocument.Id);
                return;
            }

            // Get detailed information from ICM API and check tags
            var incident = await icmApiClient.GetIncidentAsync(incidentDocument.Id);
            if (incident == null)
            {
                logger.LogInternalWarning("[IcmScanner] Could not retrieve incident details for {incidentId}", incidentDocument.Id);
                return;
            }

            // Skip if AgentProcessed tag exists
            if (incident.Tags?.Any(tag => tag.Equals("AgentProcessed", StringComparison.OrdinalIgnoreCase)) == true)
            {
                logger.LogInternalInformation("[IcmScanner] Incident {incidentId} already processed by agent (has AgentProcessed tag)", incidentDocument.Id);
                return;
            }

            // Check if OwningTeam matches (double-check even though ICM API already filtered)
            if (!string.Equals(incident.OwningTeam, filterDocument.OwningTeamId, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInternalInformation("[IcmScanner] Incident {incidentId} owning team '{owningTeamId}' does not match filter team '{filterTeamId}'", 
                    incidentDocument.Id, incident.OwningTeam, filterDocument.OwningTeamId);
                return;
            }

            logger.LogInternalInformation("[IcmScanner] Incident {incidentId} qualifies for automated RCA execution via team filter", incidentDocument.Id);
            
            // Execute Automated RCA
            await ExecuteAutomatedRCAAsync(incidentDocument);
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[IcmScanner] Error processing team-specific incident {incidentId}", incidentDocument.Id);
        }
    }

    /// <summary>
    /// Adds a tag to ICM incident or posts tag info to thread based on ReadOnly setting
    /// </summary>
    /// <param name="incidentId">The incident ID</param>
    /// <param name="threadId">The thread ID (used when ReadOnly is true)</param>
    /// <param name="tag">The tag to add</param>
    private async Task AddTagToICMOrThreadAsync(string incidentId, Guid threadId, string tag)
    {
        try
        {
            if (IsICMAPIReadOnly)
            {
                // Post tag information to thread instead of ICM when in read-only mode
                logger.LogInternalInformation("[IcmScanner] ICM API is read-only, posting tag info to thread {threadId} instead of tagging incident {incidentId}", 
                    threadId, incidentId);
                
                var tagMessage = $"**[ICM TAG]** (Incident: {incidentId})\n\n🏷️ Would add tag: **{tag}**\n\n*Note: This is shown here because ICM API is in read-only mode.*";
                await agentOutboundCommunicationService.AppendAgentStreamMessage(threadId, tagMessage, null);
            }
            else
            {
                // Add tag to ICM normally
                logger.LogInternalInformation("[IcmScanner] Adding tag '{tag}' to ICM incident {incidentId}", tag, incidentId);
                await icmPlugin.AddTagToIncident(incidentId, tag);
            }
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[IcmScanner] Error adding tag '{tag}' to incident {incidentId}", tag, incidentId);
        }
    }

    /// <summary>
    /// Executes automated RCA for the given incident
    /// </summary>
    /// <param name="incidentDocument">The incident document to process</param>
    private async Task ExecuteAutomatedRCAAsync(IcmIncidentDocument incidentDocument)
    {
        try
        {
            logger.LogInternalInformation("[IcmScanner] Starting automated RCA execution for incident {incidentId}", incidentDocument.Id);

            // Mark incident as processed to avoid duplicate processing
            _processedIncidents.Add(incidentDocument.Id);

            // Create thread that will trigger agent execution with the incident ID
            var (thread, agentContext) = await agentInboundCommunicationService.CreateAgentThread(
                title: $"🤖 Automated RCA for ICM {incidentDocument.Id}: {incidentDocument.Title}",
                message: $"Please analyze and route IncidentId {incidentDocument.Id} for RCA analysis.",
                agentTypeEnum: AgentTypeEnum.Incident,
                source: ThreadSource.Agent,
                incidentId: incidentDocument.Id,
                incidentSource: new IncidentSource(Agent.Core.Models.Api.v1.IncidentType.Icm, incidentDocument.Id)
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
            _ = Task.Run(() => MonitorRCACompletionAsync(incidentDocument.Id, thread.Id, threadUrl));
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[IcmScanner] Error executing automated RCA for incident {incidentId}", incidentDocument.Id);
            
            // Remove from processed incidents on error
            _processedIncidents.Remove(incidentDocument.Id);
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
}

public class NullableIncidentScanner : IIncidentScanner
{
    public Task ScanAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(0);
    }
}
