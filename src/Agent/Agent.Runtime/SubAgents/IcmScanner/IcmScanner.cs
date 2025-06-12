using System.Net;
using Agent.Core.Configuration;
using Agent.Core.Models.ICM;
using Agent.Core.Services;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Logging;
using Agent.Runtime.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.IcmScanner;
public class IcmScanner(ILogger<IcmScanner> logger, IICMAPIClient icmApiClient, CosmosClient cosmosClient,
                              CosmosDBSettings cosmosDbSettings):IIncidentScanner
{
    
    private readonly Container container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
    private const uint PageSize = 10;
    private readonly static TimeSpan ScanInterval = TimeSpan.FromMinutes(1);
    private DateTime lastScanTime;

    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        // First iteration scanning incidents from the last 90 days,
        // From 2nd iteration, it will continue scanning from the current time when previous scan finished.
        lastScanTime = DateTime.UtcNow.AddDays(-90);
        while (!cancellationToken.IsCancellationRequested)
        {
            await ScannAllIncidentsAsync(cancellationToken);

            await Task.Delay(ScanInterval, cancellationToken);

            lastScanTime = DateTime.UtcNow;
        }
    }

    private async Task ScannAllIncidentsAsync(CancellationToken cancellationToken)
    {
        uint page = 0;
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInternalInformation("Cancellation requested, stopping the IcM scanner.");
                return;
            }
            uint offset = page * PageSize;
            try
            {
                logger.LogInternalInformation("Scanning IcM incidents, page {page}", page);
                var response = await icmApiClient.GetIncidentsAsync(limit: PageSize, offset: offset, lastScanTime);
                if (response is null || response.Count == 0)
                {
                    logger.LogInternalInformation("No more incidents to process for IcM, stopping the scanner.");
                    return;
                }

                foreach (var incident in response)
                {
                    var incidentDocument = await GetDocumentAsync<IcmIncidentDocument>(incident.IncidentId, incident.IncidentId);

                    incidentDocument = await UpsertIncidentDocumentIfNeededAsync(incidentDocument, incident, cancellationToken);

                    //await NotifyUserAsync(incidentDocument, realtedResourceIds);
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
            else if(incident is not null && incidentDocument is not null && incidentDocument.Id == incident.IncidentId)
            {
                //var patchOperationList = new List<PatchOperation>();
                //// PatchOperation.Add is used to update existing fields or add new fields if they don't exist.
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

}

public class NullableIncidentScanner : IIncidentScanner
{
    public Task ScanAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(0);
    }
}
