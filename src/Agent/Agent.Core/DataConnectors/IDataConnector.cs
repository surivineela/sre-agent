// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using Agent.Core.Configuration;

namespace Agent.Core.DataConnectors;

public interface IDataConnector
{
    /// <summary>
    /// How often the connector <see cref="RunAsync"/> method should run. Minimum allowed interval is 5 seconds.
    /// </summary>
    TimeSpan Interval { get; }

    /// <summary>
    /// Called one time after the data connector is instantiated, but before RunAsync is called.
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    Task InitAsync(DataConnectorSettings settings, CancellationToken stoppingToken);

    /// <summary>
    /// Performs the work of the data connector. This method is called periodically based on the <see cref="Interval"/> property.
    /// </summary>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    Task RunAsync(CancellationToken stoppingToken);
}
