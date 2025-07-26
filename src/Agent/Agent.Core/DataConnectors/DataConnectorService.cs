// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using Agent.Core.Clients.Storage;
using Agent.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Core.DataConnectors;

/// <summary>
/// Background service that manages and runs multiple data connectors based on their configuration settings.
/// </summary>
public class DataConnectorService : BackgroundService
{
    private readonly ILogger<DataConnectorService> _logger;
    private readonly IReadOnlyList<DataConnectorInstance> _dataConnectors;
    private readonly DataConnectorSettings _dataConnectorSettings;
    private readonly IAzureBlobStorageClient _azureBlobStorageClient;
    private readonly DataConnectorIndexProvider _dataConnectorIndexProvider;

    public DataConnectorService(
        IEnumerable<DataConnectorInstance> dataConnectorInstances,
        DataConnectorSettings dataConnectorSettings,
        DataConnectorIndexProvider dataConnectorIndex,
        IAzureBlobStorageClient azureBlobStorageClient,
        ILogger<DataConnectorService> logger)
    {
        _logger = logger;
        _dataConnectors = dataConnectorInstances.ToList();
        _dataConnectorSettings = dataConnectorSettings;
        _azureBlobStorageClient = azureBlobStorageClient;
        _dataConnectorIndexProvider = dataConnectorIndex;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_dataConnectors.Count == 0)
        {
            _logger.LogInternalWarning("No data connectors configured. Exiting data connector service.");
            return;
        }

        foreach (KeyValuePair<string, DataConnectorTypeSettings> dataConnectorTypeSettings in _dataConnectorSettings.Types)
        {
            if (dataConnectorTypeSettings.Value.Storage == null)
            {
                _logger.LogInternalInformation("No storage settings found for data connector type {Connector}", dataConnectorTypeSettings.Key);
                continue;
            }

            _logger.LogInternalInformation("Setting up storage for data connector type {Connector}", dataConnectorTypeSettings.Key);
            await _azureBlobStorageClient.CreateContainerIfNotExistAsync(dataConnectorTypeSettings.Value.Storage.BlobStorageContainerName, Azure.Storage.Blobs.Models.PublicAccessType.None);

            if (dataConnectorTypeSettings.Value.Search == null)
            {
                _logger.LogInternalInformation("No search settings found for data connector type {Connector}", dataConnectorTypeSettings.Key);
                continue;
            }

            _logger.LogInternalInformation("Setting up search index for data connector type {Connector}", dataConnectorTypeSettings.Key);

            DataConnectorIndex dataConnectorIndex = _dataConnectorIndexProvider.GetDataConnectorIndex(dataConnectorTypeSettings.Key);

            await dataConnectorIndex.CreateOrUpdateIndex();
        }

        _logger.LogInternalInformation($"Starting {_dataConnectors.Count} data connectors.");

        List<Task> tasks = new List<Task>(_dataConnectors.Count);
        foreach (DataConnectorInstance dataConnector in _dataConnectors)
        {
            tasks.Add(Task.Run(async () => await RunDataConnectorAsync(dataConnector, stoppingToken), stoppingToken));
        }

        _logger.LogInternalInformation($"Started {_dataConnectors.Count} data connectors. Waiting for application shutdown.");

        await Task.WhenAll(tasks);
    }

    private async Task RunDataConnectorAsync(DataConnectorInstance instance, CancellationToken stoppingToken)
    {
        string implementationTypeName = instance.DataConnector.GetType().Name;
        _logger.LogInternalInformation("Initializing data connector: {Name}, {DataConnectorType}, {ImplementationType}", instance.InstanceSettings.Name, instance.InstanceSettings.DataConnectorType, implementationTypeName);

        await instance.DataConnector.InitAsync(instance.InstanceSettings, instance.TypeSettings, stoppingToken);

        TimeSpan interval = instance.DataConnector.Interval;
        if (interval < TimeSpan.FromSeconds(5))
        {
            interval = TimeSpan.FromSeconds(5);
        }

        while (true)
        {
            try
            {
                _logger.LogInternalInformation("Running data connector: {Name}, {DataConnectorType}, {ImplementationType}", instance.InstanceSettings.Name, instance.InstanceSettings.DataConnectorType, implementationTypeName);

                await instance.DataConnector.RunAsync(stoppingToken);

                _logger.LogInternalInformation("Data connector iteration completed successfully. {Name}, {DataConnectorType}, {ImplementationType}", instance.InstanceSettings.Name, instance.InstanceSettings.DataConnectorType, implementationTypeName);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == stoppingToken)
            {
                _logger.LogInternalInformation("Data connector shutting down. {Name}, {DataConnectorType}, {ImplementationType}", instance.InstanceSettings.Name, instance.InstanceSettings.DataConnectorType, implementationTypeName);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error during data connector iteration: {Message}, {Name}, {DataConnectorType}, {ImplementationType}", ex.Message, instance.InstanceSettings.Name, instance.InstanceSettings.DataConnectorType, implementationTypeName);
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
