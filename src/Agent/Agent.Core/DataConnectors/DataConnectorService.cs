// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

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
    private readonly DataConnectorIndex _dataConnectorIndex;

    public DataConnectorService(
        IEnumerable<DataConnectorInstance> dataConnectorInstances,
        DataConnectorIndex dataConnectorIndex,
        ILogger<DataConnectorService> logger)
    {
        _logger = logger;
        _dataConnectors = dataConnectorInstances.ToList();
        _dataConnectorIndex = dataConnectorIndex;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_dataConnectors.Count == 0)
        {
            _logger.LogInternalWarning("No data connectors configured. Exiting data connector service.");
            return;
        }

        _logger.LogInternalInformation($"Creating index for data connectors.");

        await _dataConnectorIndex.CreateOrUpdateIndex();

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

        await instance.DataConnector.InitAsync(instance.InstanceSettings, stoppingToken);

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

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == stoppingToken)
            {
                _logger.LogInternalInformation("Data connector shutting down. {Name}, {DataConnectorType}, {ImplementationType}", instance.InstanceSettings.Name, instance.InstanceSettings.DataConnectorType, implementationTypeName);
                return;
            }
        }
    }
}
