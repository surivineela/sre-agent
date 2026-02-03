// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

public interface IConnectorResolver
{
    T GetConnectorFromSettings<T>(string connectorName, string connectorType, string dataSource) where T : DataConnectorDefinitionBase, new();

    List<DataConnectorBasicInfo> GetAllDataConnectors();

    string? GetAgentSpaceDataConnectorIdentity();
}

/// <summary>
/// Basic information about a data connector
/// </summary>
public class DataConnectorBasicInfo
{
    /// <summary>
    /// Name of the data connector
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of the data connector
    /// </summary>
    public string ConnectorType { get; set; } = string.Empty;

    /// <summary>
    /// Data source connection string or URI
    /// </summary>
    public string DataSource { get; set; } = string.Empty;

    /// <summary>
    /// Managed identity resource ID for authentication
    /// </summary>
    public string Identity { get; set; } = string.Empty;

    /// <summary>
    /// Source of the data connector (Agent or AgentSpace)
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Extended properties for the data connector
    /// </summary>
    public Dictionary<string, System.Text.Json.JsonElement>? ExtendedProperties { get; set; }

    /// <summary>
    /// Indicates whether to use Managed Identity as Federated Identity Credential (FIC).
    /// </summary>
    public bool UseManagedIdentityAsFic { get; set; }

    /// <summary>
    /// The federated client ID for FIC authentication.
    /// </summary>
    public string? FederatedClientId { get; set; }

    /// <summary>
    /// The federated tenant ID for FIC authentication.
    /// </summary>
    public string? FederatedTenantId { get; set; }
}
