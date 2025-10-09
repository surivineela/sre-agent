// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Web.Models.ExtendedAgents.Request;

/// <summary>
/// Request model for testing a Kusto query
/// </summary>
public class KustoQueryTestRequest
{
    /// <summary>
    /// The Kusto query to execute
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Name of the connector to use
    /// </summary>
    public string Connector { get; set; } = string.Empty;

    /// <summary>
    /// Kusto database name
    /// </summary>
    public string Database { get; set; } = string.Empty;

    /// <summary>
    /// Query mode (query or management)
    /// </summary>
    public string Mode { get; set; } = "query";

    /// <summary>
    /// Parameters to substitute in the query (e.g., {"SubscriptionId": "guid-here"})
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();
}
