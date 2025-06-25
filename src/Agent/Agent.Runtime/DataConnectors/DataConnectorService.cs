// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.DataConnectors;

/// <summary>
/// Background service that manages and runs multiple data connectors based on their configuration settings.
/// </summary>
public class DataConnectorService : BackgroundService
{
    private readonly ILogger<DataConnectorService> _logger;
    private readonly IReadOnlyList<DataConnectorInstance> _dataConnectors;

    public DataConnectorService(IEnumerable<DataConnectorInstance> dataConnectors, ILogger<DataConnectorService> logger)
    {
        _logger = logger;
        _dataConnectors = dataConnectors.ToList();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_dataConnectors.Count == 0)
        {
            _logger.LogInternalWarning("No data connectors configured. Exiting data connector service.");
            return;
        }

        _logger.LogInternalInformation($"Starting {_dataConnectors.Count} data connectors.");

        List<Task> tasks = new List<Task>(_dataConnectors.Count);

        foreach(DataConnectorInstance dataConnector in _dataConnectors)
        {
            tasks.Add(Task.Run(async () => await RunDataConnectorAsync(dataConnector, stoppingToken), stoppingToken));
        }

        _logger.LogInternalInformation($"Started {_dataConnectors.Count} data connectors. Waiting for application shutdown.");

        await Task.WhenAll(tasks);
    }

    private async Task RunDataConnectorAsync(DataConnectorInstance instance, CancellationToken stoppingToken)
    {
        string implementationTypeName = instance.DataConnector.GetType().Name;
        _logger.LogInternalInformation("Initializing data connector: {DataConnectorName}, {DataConnectorType}, {ImplementationType}", instance.Settings.DataConnectorName, instance.Settings.DataConnectorType, implementationTypeName);

        await instance.DataConnector.InitAsync(instance.Settings, stoppingToken);

        while (true)
        {
            try
            {
                _logger.LogInternalInformation("Running data connector: {DataConnectorName}, {DataConnectorType}, {ImplementationType}", instance.Settings.DataConnectorName, instance.Settings.DataConnectorType, implementationTypeName);

                await instance.DataConnector.RunAsync(stoppingToken);

                TimeSpan interval = instance.DataConnector.Interval;
                if (interval < TimeSpan.FromSeconds(5))
                {
                    interval = TimeSpan.FromSeconds(5);
                }

                await Task.Delay(interval, stoppingToken);

                _logger.LogInternalInformation("Data connector iteration completed successfully: {DataConnectorName}, {DataConnectorType}, {ImplementationType}", instance.Settings.DataConnectorName, instance.Settings.DataConnectorType, implementationTypeName);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == stoppingToken)
            {
                _logger.LogInternalInformation("Data connector  {DataConnectorName}, {DataConnectorType}, {ImplementationType}", instance.Settings.DataConnectorName, instance.Settings.DataConnectorType, implementationTypeName);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error during data connector iteration: {Message}, {DataConnectorName}, {DataConnectorType}, {ImplementationType}", ex.Message, instance.Settings.DataConnectorName, instance.Settings.DataConnectorType, implementationTypeName);
            }
        }
    }
}
