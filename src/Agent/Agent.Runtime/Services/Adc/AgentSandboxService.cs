// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Adc;
using Agent.Data;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Diagnostics.Tracing.Parsers.MicrosoftWindowsTCPIP;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services.Adc;

/// <summary>
/// High-level service for managing agent sandboxes.
/// Orchestrates <see cref="IAdcApiClient"/> for ADC operations and Cosmos DB for persistence.
/// </summary>
public class AgentSandboxService : IAgentSandboxService
{
    private readonly IAdcApiClient _adcApiClient;
    private readonly CosmosClient _cosmosClient;
    private readonly AdcSettings _adcSettings;
    private readonly string _databaseName;
    private readonly ILogger<AgentSandboxService> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _endpointCache = new();

    public AgentSandboxService(
        IAdcApiClient adcApiClient,
        CosmosClient cosmosClient,
        AdcSettings adcSettings,
        CosmosDBSettings cosmosDbSettings,
        ILogger<AgentSandboxService> logger,
        IHostEnvironment hostEnvironment)
    {
        _adcApiClient = adcApiClient;
        _cosmosClient = cosmosClient;
        _adcSettings = adcSettings;
        _databaseName = cosmosDbSettings.Docs.Database;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    private Container GetContainer() =>
        _cosmosClient.GetContainer(_databaseName, AgentDataConfiguration.ThreadContainerName);

    #region Base Snapshot Management

    public async Task<AdcBaseSnapshotInfo> EnsureBaseSnapshotAsync(ISandboxInitializer? initializer = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Ensuring ADC base snapshot exists and is current...");

        var existingSnapshotDoc = await GetLatestBaseSnapshotDocumentAsync(cancellationToken);

        // Check if we need to create/update the base snapshot
        if (existingSnapshotDoc != null && existingSnapshotDoc.BaseImage == _adcSettings.BaseImage)
        {
            _logger.LogInternalInformation("Base snapshot is current. SnapshotId={SnapshotId}", existingSnapshotDoc.SnapshotId);
            return existingSnapshotDoc.ToInfo();
        }

        _logger.LogInternalInformation("Base image changed or no snapshot exists. Creating new base snapshot...");

        // Step 1: Create disk image
        var diskImageId = await _adcApiClient.CreateDiskImageAsync(_adcSettings.BaseImage, cancellationToken);
        _logger.LogInternalInformation("Created disk image: {DiskImageId}", diskImageId);

        // Step 2: Wait for disk image to be ready
        await _adcApiClient.WaitForDiskImageReadyAsync(diskImageId, cancellationToken);

        // Step 3: Create sandbox from disk image
        var defaultResources = GetDefaultResources();
        var sandboxResponse = await _adcApiClient.CreateSandboxFromDiskImageAsync(diskImageId, defaultResources, cancellationToken);
        _logger.LogInternalInformation("Created sandbox: {SandboxId}", sandboxResponse.Id);

        // Step 4: Wait for sandbox to be running
        await _adcApiClient.WaitForSandboxRunningAsync(sandboxResponse.Id, cancellationToken);

        // Step 5: Configure ports if any are specified
        if (_adcSettings.DefaultPorts.Count > 0)
        {
            _logger.LogInternalInformation("Configuring {PortCount} ports for base sandbox {SandboxId}", _adcSettings.DefaultPorts.Count, sandboxResponse.Id);
            await ConfigureDefaultPortsAsync(sandboxResponse.Id, cancellationToken);
        }

        // Step 6: Initialize the base sandbox if an initializer is provided
        if (initializer != null)
        {
            _logger.LogInternalInformation("Running initialization for base sandbox {SandboxId}...", sandboxResponse.Id);
            sandboxResponse = await _adcApiClient.GetSandboxAsync(sandboxResponse.Id, cancellationToken);
            var tempSandboxInfo = new AdcSandboxInfo
            {
                SandboxId = sandboxResponse.Id,
                ThreadId = string.Empty, // Base sandbox is not tied to a thread
                Endpoint = GetPrimaryEndpoint(sandboxResponse),
                SnapshotId = null,
                Resources = defaultResources,
                CreatedAt = DateTime.UtcNow
            };
            await initializer.InitializeAsync(tempSandboxInfo, cancellationToken);
        }

        // Step 7: Create snapshot
        var snapshotName = $"basesnapshot-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var baseSnapshotLabels = new Dictionary<string, string> { ["snapshot-type"] = "base" };
        var snapshotId = await _adcApiClient.CreateSnapshotAsync(sandboxResponse.Id, snapshotName, baseSnapshotLabels, cancellationToken);
        _logger.LogInternalInformation("Created base snapshot: {SnapshotId}", snapshotId);

        // Step 8: Delete the temporary sandbox
        await _adcApiClient.DeleteSandboxAsync(sandboxResponse.Id, cancellationToken);
        _logger.LogInternalInformation("Deleted temporary sandbox: {SandboxId}", sandboxResponse.Id);

        // Step 9: Persist the base snapshot document
        var baseSnapshotDoc = new AdcBaseSnapshotDocument(
            Id: snapshotId,
            SnapshotId: snapshotId,
            DiskImageId: diskImageId,
            BaseImage: _adcSettings.BaseImage,
            Name: snapshotName,
            Resources: defaultResources,
            CreatedAt: DateTime.UtcNow
        );

        var container = GetContainer();
        await container.CreateItemAsync(baseSnapshotDoc, new PartitionKey(baseSnapshotDoc.PartitionKey), cancellationToken: cancellationToken);

        _logger.LogInternalInformation("Base snapshot document persisted. SnapshotId={SnapshotId}", snapshotId);
        return baseSnapshotDoc.ToInfo();
    }

    public async Task<AdcBaseSnapshotInfo?> GetBaseSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var doc = await GetLatestBaseSnapshotDocumentAsync(cancellationToken);
        return doc?.ToInfo();
    }

    private async Task<AdcBaseSnapshotDocument?> GetLatestBaseSnapshotDocumentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var container = GetContainer();
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.documentType = 'AdcBaseSnapshot' ORDER BY c.createdAt DESC OFFSET 0 LIMIT 1");

            using var iterator = container.GetItemQueryIterator<AdcBaseSnapshotDocument>(
                query,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey("AdcBaseSnapshot") });

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                return response.FirstOrDefault();
            }

            return null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    #endregion

    #region Thread Sandbox Management

    public async Task<AdcSandboxInfo> CreateSandboxForThreadAsync(
        string threadId,
        string? snapshotId = null,
        CancellationToken cancellationToken = default)
    {
        // Delete any existing sandbox
        await DeleteSandboxForThreadAsync(threadId, cancellationToken);

        // If snapshotId not provided, use base snapshot
        if (snapshotId == null)
        {
            var baseSnapshotDoc = await GetLatestBaseSnapshotDocumentAsync(cancellationToken);
            if (baseSnapshotDoc == null)
            {
                throw new InvalidOperationException("Base snapshot does not exist.");
            }
            snapshotId = baseSnapshotDoc.SnapshotId;
        }

        return await CreateSandboxFromSnapshotInternalAsync(threadId, snapshotId, cancellationToken);
    }

    private async Task<AdcSandboxInfo> CreateSandboxFromSnapshotInternalAsync(
        string threadId,
        string snapshotId,
        CancellationToken cancellationToken)
    {
        _logger.LogInternalInformation("Creating new sandbox for thread {ThreadId} from snapshot {SnapshotId}", threadId, snapshotId);

        // Provision via ADC API (resources inherited from snapshot)
        var labels = new Dictionary<string, string> { ["thread-id"] = threadId };
        var sandboxResponse = await _adcApiClient.CreateSandboxFromSnapshotAsync(snapshotId, labels, cancellationToken);
        var sandboxId = sandboxResponse.Id;

        try
        {
            await _adcApiClient.WaitForSandboxRunningAsync(sandboxId, cancellationToken);

            // Configure ports if any are specified
            if (_adcSettings.DefaultPorts.Count > 0)
            {
                _logger.LogInternalInformation("Configuring {PortCount} ports for sandbox {SandboxId}", _adcSettings.DefaultPorts.Count, sandboxId);
                await ConfigureDefaultPortsAsync(sandboxId, cancellationToken);
            }

            sandboxResponse = await _adcApiClient.GetSandboxAsync(sandboxId, cancellationToken);

            // Persist Record
            var sandboxDoc = new AdcThreadSandboxDocument(
                Id: threadId,
                ThreadId: threadId,
                SandboxId: sandboxId,
                Endpoint: GetPrimaryEndpoint(sandboxResponse),
                SourceSnapshotId: snapshotId,
                Resources: GetDefaultResources(),
                CreatedAt: DateTime.UtcNow,
                LastAccessedAt: DateTime.UtcNow
            );

            var container = GetContainer();
            await container.UpsertItemAsync(sandboxDoc, new PartitionKey(sandboxDoc.PartitionKey), cancellationToken: cancellationToken);

            _logger.LogInternalInformation("Sandbox {SandboxId} ready for thread {ThreadId}", sandboxId, threadId);

            // Cache the endpoint
            _endpointCache[threadId] = sandboxDoc.Endpoint;

            return sandboxDoc.ToInfo();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to setup sandbox {SandboxId} for thread {ThreadId}. Cleaning up...", sandboxId, threadId);

            // Best effort cleanup
            try
            {
                await _adcApiClient.DeleteSandboxAsync(sandboxId, CancellationToken.None);
                _logger.LogInternalInformation("Cleaned up failed sandbox {SandboxId}", sandboxId);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogInternalWarning(cleanupEx, "Failed to cleanup sandbox {SandboxId}", sandboxId);
            }

            throw;
        }
    }

    public async Task<AdcSandboxInfo?> GetSandboxForThreadAsync(string threadId, CancellationToken cancellationToken = default)
    {
        var doc = await GetSandboxDocumentForThreadAsync(threadId, cancellationToken);
        return doc?.ToInfo();
    }

    public async Task<string> GetSandboxEndpointAsync(string threadId, CancellationToken cancellationToken = default)
    {
        if (_hostEnvironment.IsDevelopment() && !string.IsNullOrEmpty(_adcSettings.LocalSandboxEndpoint))
        {
            return _adcSettings.LocalSandboxEndpoint;
        }

        // Check local cache first
        if (_endpointCache.TryGetValue(threadId, out var cachedEndpoint))
        {
            return cachedEndpoint;
        }

        // Try to get existing sandbox
        var sandboxInfo = await GetSandboxForThreadAsync(threadId, cancellationToken);
        if (sandboxInfo != null)
        {
            _endpointCache[threadId] = sandboxInfo.Endpoint;
            return sandboxInfo.Endpoint;
        }

        // Create new sandbox from base snapshot
        sandboxInfo = await CreateSandboxForThreadAsync(threadId, snapshotId: null, cancellationToken);
        return sandboxInfo.Endpoint;
    }

    private async Task<AdcThreadSandboxDocument?> GetSandboxDocumentForThreadAsync(string threadId, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = GetContainer();
            var response = await container.ReadItemAsync<AdcThreadSandboxDocument>(
                threadId,
                new PartitionKey("AdcThreadSandbox"),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteSandboxForThreadAsync(string threadId, CancellationToken cancellationToken = default)
    {
        // Invalidate cache
        _endpointCache.TryRemove(threadId, out _);

        var sandboxDoc = await GetSandboxDocumentForThreadAsync(threadId, cancellationToken);
        if (sandboxDoc == null)
        {
            return;
        }

        _logger.LogInternalInformation("Deleting sandbox {SandboxId} for thread {ThreadId}", sandboxDoc.SandboxId, threadId);

        // Best effort clean up remote sandbox
        try
        {
            await _adcApiClient.DeleteSandboxAsync(sandboxDoc.SandboxId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to delete remote sandbox {SandboxId}. Removing local record anyway.", sandboxDoc.SandboxId);
        }

        // Remove DB Record
        var container = GetContainer();
        await container.DeleteItemAsync<AdcThreadSandboxDocument>(
            threadId,
            new PartitionKey("AdcThreadSandbox"),
            cancellationToken: cancellationToken);
    }

    public async Task StartSandboxAsync(string threadId, CancellationToken cancellationToken = default)
    {
        var sandboxDoc = await GetSandboxDocumentForThreadAsync(threadId, cancellationToken)
            ?? throw new InvalidOperationException($"No sandbox found for thread {threadId}");

        await _adcApiClient.StartSandboxAsync(sandboxDoc.SandboxId, cancellationToken);
        _logger.LogInternalInformation("Started sandbox for thread {ThreadId}", threadId);
    }

    public async Task StopSandboxAsync(string threadId, CancellationToken cancellationToken = default)
    {
        var sandboxDoc = await GetSandboxDocumentForThreadAsync(threadId, cancellationToken)
            ?? throw new InvalidOperationException($"No sandbox found for thread {threadId}");

        await _adcApiClient.StopSandboxAsync(sandboxDoc.SandboxId, cancellationToken);
        _logger.LogInternalInformation("Stopped sandbox for thread {ThreadId}", threadId);
    }

    #endregion

    #region Thread Snapshot Management

    public async Task<AdcSnapshotInfo> CreateSnapshotAsync(string threadId, string snapshotName, CancellationToken cancellationToken = default)
    {
        var sandboxDoc = await GetSandboxDocumentForThreadAsync(threadId, cancellationToken)
            ?? throw new InvalidOperationException($"No active sandbox found for thread {threadId} to snapshot.");

        _logger.LogInternalInformation("Snapshotting sandbox {SandboxId} for thread {ThreadId}", sandboxDoc.SandboxId, threadId);

        // 1. Create snapshot via ADC API
        var labels = new Dictionary<string, string>
        {
            ["thread-id"] = threadId,
            ["snapshot-type"] = "thread"
        };
        var snapshotId = await _adcApiClient.CreateSnapshotAsync(sandboxDoc.SandboxId, snapshotName, labels, cancellationToken);

        // 2. Track the original base snapshot
        string originalBase = sandboxDoc.SourceSnapshotId ?? "unknown";

        var snapshotDoc = new AdcThreadSnapshotDocument(
            Id: snapshotId,
            ThreadId: threadId,
            SnapshotId: snapshotId,
            Name: snapshotName,
            OriginalBaseSnapshotId: originalBase,
            CreatedAt: DateTime.UtcNow
        );

        var container = GetContainer();
        await container.CreateItemAsync(snapshotDoc, new PartitionKey(snapshotDoc.PartitionKey), cancellationToken: cancellationToken);

        _logger.LogInternalInformation("Snapshot {SnapshotId} created for thread {ThreadId}", snapshotId, threadId);
        return snapshotDoc.ToInfo();
    }

    public async Task<IEnumerable<AdcSnapshotInfo>> GetSnapshotsForThreadAsync(string threadId, CancellationToken cancellationToken = default)
    {
        var container = GetContainer();
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.threadId = @threadId AND c.documentType = 'AdcThreadSnapshot'")
            .WithParameter("@threadId", threadId);

        var snapshots = new List<AdcSnapshotInfo>();
        using var iterator = container.GetItemQueryIterator<AdcThreadSnapshotDocument>(query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey("AdcThreadSnapshot") });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            snapshots.AddRange(response.Select(d => d.ToInfo()));
        }

        return snapshots;
    }

    public async Task DeleteSnapshotAsync(string threadId, string snapshotId, CancellationToken cancellationToken = default)
    {
        var container = GetContainer();
        try
        {
            await container.DeleteItemAsync<AdcThreadSnapshotDocument>(snapshotId, new PartitionKey("AdcThreadSnapshot"), cancellationToken: cancellationToken);
            _logger.LogInternalInformation("Deleted snapshot {SnapshotId}", snapshotId);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("Snapshot {SnapshotId} not found to delete.", snapshotId);
        }
    }

    #endregion

    #region Helpers

    private AdcResources GetDefaultResources() => new()
    {
        Cpu = _adcSettings.DefaultCpu,
        Memory = _adcSettings.DefaultMemory,
        Disk = _adcSettings.DefaultDisk
    };

    private async Task ConfigureDefaultPortsAsync(string sandboxId, CancellationToken cancellationToken)
    {
        var portSpecs = _adcSettings.DefaultPorts.Select(p => new AdcPortSpec
        {
            Port = p.Port,
            Name = p.Name,
            Protocol = p.Protocol
        });
        await _adcApiClient.UpdatePortsAsync(sandboxId, portSpecs, cancellationToken);
    }

    private static string GetPrimaryEndpoint(AdcSandboxResponse sandbox)
    {
        return sandbox.Ports?.FirstOrDefault()?.Url ?? string.Empty;
    }

    #endregion
}
