using System.Net;
using System.Threading;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Models.ICM;
using Agent.Core.Services;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Logging;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.IcmScanner;
public class IcmScanner(ILogger<IcmScanner> logger,
    IICMAPIClient icmApiClient,
    CosmosClient cosmosClient,
    CosmosDBSettings cosmosDbSettings,
    IIncidentHandlingService incidentHandlingService,
    IIncidentHandlerManagementService incidentHandlerManagementService,
    IIncidentManagementService<IcmIncidentDocument> incidentManagementService,
    IIncidentFilterManagementService incidentFilterManagementService,
    IAgentInboundCommunicationService agentInboundCommunicationService) : IIncidentScanner
{

    private readonly Container container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
    private const uint PageSize = 10;
    private readonly static TimeSpan ScanInterval = TimeSpan.FromMinutes(1);
    private DateTime lastScanTime;
    //After offset > 5000, ICM endpoint will returning 400 bad request
    //Updating offset to 200, since now it will apply on every existing incident Filter
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
                logger.LogInternalInformation("No incident filters found, skipping IcM scanner.");
            } else
            {
                logger.LogInternalInformation("Found {filterCount} incident filters, starting IcM scanner.", filters.Count);
                await ScannAllIncidentsAsync(cancellationToken,filters);
                lastScanTime = await UpdateLastScanTimeDocAsync(DateTime.UtcNow);
            }
            await Task.Delay(ScanInterval, cancellationToken);
        }
    }
    private async Task ScannAllIncidentsAsync(CancellationToken cancellationToken, List<IncidentFilterDocument> filters)
    {
        
        
        foreach (var filter in filters)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInternalInformation("Cancellation requested, stopping the IcM scanner.");
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
                    logger.LogInternalError(ex, "Error scanning incidents for filter {filterId}", filterDocument.Id);
                }
            }
        }
    }

    private async Task ScanIncidentsForFilter(IncidentFilterDocument filterDocument, CancellationToken cancellationToken)
    {
        logger.LogInternalInformation("Scanning incidents for filter {filterId}", filterDocument.Id);
        uint page = 0;
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInternalInformation("Cancellation requested, stopping the IcM scanner.");
                return;
            }
            uint offset = page * PageSize;

            if (offset > maxOffset)
            {
                logger.LogInternalInformation("Stop scanning ICMs over {offset}", offset);
                return;
            }

            try
            {
                logger.LogInternalInformation("Scanning IcM incidents, page {page}, lastScanTime {lastScanTime}", page, lastScanTime);
                var incidents = await icmApiClient.GetIncidentsAsync(PageSize, offset, lastScanTime, null, filterDocument.TitleContains);
                if (incidents is null || incidents.Count == 0)
                {
                    logger.LogInternalInformation("No incidents found for filter {filterId}", filterDocument.Id);
                    return;
                }
                foreach (var incident in incidents)
                {
                    var incidentDocument = await GetDocumentAsync<IcmIncidentDocument>(incident.IncidentId, incident.IncidentId);
                    incidentDocument = await UpsertIncidentDocumentIfNeededAsync(incidentDocument, incident);
                    await NotifyUserAsync(incidentDocument, new List<string>());
                }
                //Between each page, wait for 1 minute
                await Task.Delay(ScanInterval);
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error scanning IcM incidents");
            }
            page++;
        }
    }

    private async Task<T> GetDocumentAsync<T>(string id, string partitionKey) where T : ICosmosDocument
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

    private async Task<IcmIncidentDocument> UpsertIncidentDocumentIfNeededAsync(IcmIncidentDocument incidentDocument, Incident incident, CancellationToken cancellationToken = default)
    {
        try
        {
            if (incidentDocument is null && incident is not null)
            {
                logger.LogInternalInformation("Creating new incident document for IcM by id {incidentId}", incident.IncidentId);

                incidentDocument = new IcmIncidentDocument(incident);
                incidentDocument = await container.CreateItemAsync(incidentDocument, new PartitionKey(incidentDocument.PartitionKey), cancellationToken: cancellationToken);

                logger.LogInternalInformation("Created new incident document for IcM incident {incidentId}", incident.IncidentId);
            }
            else if (incident is not null && incidentDocument is not null && incidentDocument.Id == incident.IncidentId)
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
                logger.LogInternalInformation("Upserting existing incident document for IcM incident {incidentId}", incident.IncidentId);
                incidentDocument = await container.UpsertItemAsync(updatedDoc, new PartitionKey(updatedDoc.PartitionKey), cancellationToken: cancellationToken);
            }

            return incidentDocument;

        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error upserting incident document for IcM incident {incidentId}", incident.IncidentId);
        }
        return incidentDocument;
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
            logger.LogInternalError(ex, "Error updating LastScanTime for IcmScanner");
            return DateTime.UtcNow;
        }
    }

    private async Task NotifyUserAsync(IcmIncidentDocument incidentDocument, List<string> relatedResourceIds)
    {
        try
        {
            if (incidentDocument is null)
            {
                logger.LogInternalWarning("Incident document is null, skipping notification.");
                return;
            }

            if (incidentDocument.Status.Equals("resolved", StringComparison.OrdinalIgnoreCase) || incidentDocument.Status.Equals("mitigated", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInternalInformation("Incident {incidentId} is mitigated/resolved, skipping notification.", incidentDocument.Id);
                return;
            }

            var threadDocument = await GetIncidentThread(incidentDocument.Id);
            if (threadDocument is null)
            {
                logger.LogInternalInformation("Thread doesn't exist for incident {incidentId}, skipping notification", incidentDocument.Id);
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
                logger.LogInternalInformation("Thread already exists for incident {incidentId}, checking whether it needs to be updated", incidentDocument.Id);
                var existingIncidentDocument = await incidentManagementService.GetIncidentDetails(incidentDocument.Id);
                var existingDiscussionEntries = existingIncidentDocument != null ? existingIncidentDocument.DiscussionEntries : new List<DiscussionEntry>();
                var latestDiscussionEntries = await icmApiClient.GetIncidentDiscussionEntriesAsync(incidentDocument.Id);

                var newNotes = latestDiscussionEntries
                        .Skip(existingDiscussionEntries.Count)
                        .Select(entry => new IncidentDiscussion(entry.IncidentId, entry.Text, entry.ChangedBy, entry.ChangedBy, entry.Date))
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
                logger.LogInternalWarning("Multiple threads({threadIds}) found for incident {incidentId}, returning the first one.", string.Join(',', response.Select(t => t.Id)), incidentId);
                return response.FirstOrDefault();
            }
        }
        return null;
    }
}

public class NullableIncidentScanner : IIncidentScanner
{
    public Task ScanAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(0);
    }
}
