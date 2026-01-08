// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Agent.Runtime.Models;

/// <summary>
/// Generic status values for data connectors (MCP, Kusto, etc.).
/// </summary>
public enum DataConnectorStatus
{
    /// <summary>
    /// Connector/connection is being initialized.
    /// </summary>
    Initializing,

    /// <summary>
    /// Connector/connection is active and healthy.
    /// </summary>
    Connected,

    /// <summary>
    /// Connector/connection failed to initialize or encountered an error and can't be recovered.
    /// </summary>
    Failed,

    /// <summary>
    /// Connector/connection is disconnected temprarily and is recoverable.
    /// </summary>
    Disconnected
}
