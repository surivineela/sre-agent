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
    /// Connector/connection is configured and ready to use, but no active session exists.
    /// Tools are available and will establish a session on demand. (Only for stdio MCP)
    /// </summary>
    Standby,

    /// <summary>
    /// Connector/connection failed to initialize or encountered an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Connector/connection was disconnected.
    /// </summary>
    Disconnected
}
