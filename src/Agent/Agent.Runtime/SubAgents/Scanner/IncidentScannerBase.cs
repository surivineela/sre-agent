// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents.Scanner;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents;


public abstract class IncidentScannerBase<TIncidentDocument, TIncident, TIncidentFilterDocument, TIncidentFilterDocumentPayload> : IIncidentScanner
    where TIncidentDocument : IIncidentDocument
    where TIncident : class
    where TIncidentFilterDocument : TIncidentFilterDocumentPayload, IIncidentFilterDocument
    where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
{
    protected readonly IIncidentFilterManagementService<TIncidentFilterDocument, TIncidentFilterDocumentPayload> _incidentFilterManagementService;
    protected readonly ILogger _logger;

    protected readonly TimeSpan _scanInterval = TimeSpan.FromMinutes(1);
    protected readonly Container _container;
    private bool isScanSucceeded = true;
    protected DateTime lastScanTime { get; private set; }

    public IncidentScannerBase(
        Container container,
        IIncidentFilterManagementService<TIncidentFilterDocument, TIncidentFilterDocumentPayload> incidentFilterManagementService,
        ILogger logger)
    {
        _container = container;
        _logger = logger;
        _incidentFilterManagementService = incidentFilterManagementService;
    }

    /// <summary>
    /// Notify user or start a new thread for incident.
    /// </summary>
    /// <param name="incidentDocument"></param>
    /// <param name="relatedResourceIds"></param>
    /// <returns></returns>
    protected abstract Task NotifyUserIfNeededAsync(TIncidentDocument incidentDocument, TIncident incident, TIncidentFilterDocument filter, List<string> relatedResourceIds);

    /// <summary>
    /// Update or create incident document if needed.
    /// </summary>
    /// <param name="incidentDocument"></param>
    /// <param name="incident"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected abstract Task<TIncidentDocument> UpsertIncidentDocumentIfNeededAsync(TIncidentDocument? incidentDocument, TIncident incident, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calling API to get incidents based on the filter criteria.
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected abstract Task<IEnumerable<TIncident>> ScanIncidentsForFilter(TIncidentFilterDocument filter, CancellationToken cancellationToken);

    protected abstract string GetIncidentId(TIncident incident);

    /// <summary>
    /// Method to override for any post scanning operations.
    /// </summary>
    /// <returns></returns>
    protected virtual Task PostScanningAsync()
    {
        return Task.CompletedTask;
    }

    public virtual async Task ScanAsync(CancellationToken cancellationToken)
    {
        var lastScanTimeDoc = await GetDocumentAsync<LastScanTimeDoc>(LastScanTimeDoc.LastScanTimeKey, LastScanTimeDoc.LastScanTimeKey);
        lastScanTime = lastScanTimeDoc != null ? lastScanTimeDoc.LastScanTime : DateTime.UtcNow.AddDays(-30); // Default to 30 days ago if not found
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var filters = await GetIncidentFiltersAsync();
                if (filters is null || filters.Count == 0)
                {
                    _logger.LogInternalInformation($"[IncidentScannerBase] No incident filters found, skipping scan.");
                    return;
                }
                else
                {
                    _logger.LogInternalInformation("[IncidentScannerBase] Found {filterCount} incident filters, starting ServiceNow scanner.", filters.Count);
                    var scanStartTime = DateTime.UtcNow;
                    await ScanAllIncidentsAsync(filters, cancellationToken);

                    if (isScanSucceeded)
                    {
                        var adjustedLastScanTime = AdjustLastScanTime(lastScanTime);
                        lastScanTime = await UpdateLastScanTimeDocAsync(adjustedLastScanTime);
                        _logger.LogInternalInformation("[IncidentScannerBase] Scan completed successfully at {scanTime}. Last scan time updated to {lastScanTime}.", scanStartTime, lastScanTime);
                    }
                    else
                    {
                        _logger.LogInternalError("[IncidentScannerBase] Incident scan failed, will retry in the next cycle.");
                    }
                }
                await PostScanningAsync();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "[IncidentScannerBase] Error occurred during scanning process.");
            }

            await Task.Delay(_scanInterval, cancellationToken);
        }
    }

    protected async Task<X?> GetDocumentAsync<X>(string id, string partitionKey)
    {
        try
        {
            ItemResponse<X> response = await _container.ReadItemAsync<X>(
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

    protected virtual async Task<List<string>> GetRelatedResourceIdsForNotifyingUser()
    {
        return await Task.FromResult(new List<string>());
    }

    protected virtual async Task<List<TIncidentFilterDocument>> GetIncidentFiltersAsync()
    {
        return await _incidentFilterManagementService.ListIncidentFilters();
    }

    /// <summary>
    /// To adjust the last scan time, can add some overlap between each scan
    /// </summary>
    /// <param name="lastScanTime"></param>
    /// <returns></returns>
    protected virtual DateTime AdjustLastScanTime(DateTime lastScanTime)
    {
        return lastScanTime;
    }

    protected virtual async Task ScanAllIncidentsAsync(List<TIncidentFilterDocument> filters, CancellationToken cancellationToken)
    {
        IEnumerable<TIncident> incidents = [];
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInternalInformation("Cancellation requested, stopping the ServiceNow scanner.");
            return;
        }

        foreach (var filter in filters)
        {
            isScanSucceeded = true;
            try
            {
                incidents = await ScanIncidentsForFilter(filter, cancellationToken);
                if (incidents is null || !incidents.Any())
                {
                    _logger.LogInternalInformation($"No incidents found for filter {filter.Id}");
                    continue;
                }
                foreach (var incident in incidents)
                {
                    string incidentId = GetIncidentId(incident);
                    var incidentDocument = await GetDocumentAsync<TIncidentDocument>(incidentId, incidentId);

                    incidentDocument = await UpsertIncidentDocumentIfNeededAsync(incidentDocument, incident);

                    var relatedResourceIds = await GetRelatedResourceIdsForNotifyingUser();
                    await NotifyUserIfNeededAsync(incidentDocument, incident, filter, relatedResourceIds);
                }
            }
            catch (Exception ex)
            {
                isScanSucceeded = false;
                _logger.LogInternalError(ex, "Error scanning incidents for filter {filterId}", filter.Id);
                return;
            }
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

            var doc = await _container.PatchItemAsync<LastScanTimeDoc>(
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

            var doc = await _container.CreateItemAsync(lastScanTimeDoc, new PartitionKey(lastScanTimeDoc.PartitionKey));
            return doc.Resource.LastScanTime;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error updating LastScanTime for ServiceNowScanner");
            return DateTime.UtcNow;
        }
    }

    protected virtual async Task<ThreadDocument?> GetIncidentThread(string incidentId, IncidentType incidentType)
    {
        var threads = _container.GetItemLinqQueryable<ThreadDocument>()
            .Where(doc => doc.DocumentType == "Thread" && doc.Source == ThreadSource.Incident)
            .Where(doc => (doc.IncidentSource != null && doc.IncidentSource.IncidentType == incidentType && doc.IncidentSource.IncidentId == incidentId) || (doc.IncidentId == incidentId))
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
                _logger.LogInternalWarning("Multiple threads({threadIds}) found for incident {incidentId}, returning the first one.", string.Join(',', response.Select(t => t.Id)), incidentId);
                return response.FirstOrDefault();
            }
        }
        return null;
    }
}
