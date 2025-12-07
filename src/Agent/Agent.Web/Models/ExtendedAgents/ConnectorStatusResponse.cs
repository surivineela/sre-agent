// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Web.Models.ExtendedAgents;

/// <summary>
/// Status information for a connector (e.g., Kusto, MCP).
/// </summary>
public class ConnectorStatusResponse
{
    /// <summary>
    /// Connector name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Connector type (e.g., Kusto, Mcp).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if the connector is healthy.
    /// </summary>
    public bool Healthy { get; set; }

    /// <summary>
    /// Summary status message explaining health or failure reason.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional additional diagnostic details (shape varies by connector type).
    /// </summary>
    public object? Details { get; set; }

    /// <summary>
    /// Connector status. Can be one of the following: 'Initializing', 'Connected', 'Standby', 'Failed', 'Disconnected'.
    /// 'Standby' indicates the connector is configured and ready to use, but no active session exists.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Optional timing for the health probe in milliseconds.
    /// </summary>
    public long ExecutionTimeMs { get; set; } = 0;

    /// <summary>
    /// Parameterless constructor needed for serializers.
    /// </summary>
    public ConnectorStatusResponse() { }

    /// <summary>
    /// Full constructor for convenient creation.
    /// </summary>
    public ConnectorStatusResponse(string Name, string Type, bool Healthy, string Message, object? Details = null)
    {
        this.Name = Name;
        this.Type = Type;
        this.Healthy = Healthy;
        this.Message = Message;
        this.Details = Details;
    }

    /// <summary>
    /// Full constructor including status and execution time.
    /// </summary>
    public ConnectorStatusResponse(string Name, string Type, bool Healthy, string Message, string Status, long ExecutionTimeMs, object? Details = null)
    {
        this.Name = Name;
        this.Type = Type;
        this.Healthy = Healthy;
        this.Message = Message;
        this.Status = Status;
        this.ExecutionTimeMs = ExecutionTimeMs;
        this.Details = Details;
    }
}
