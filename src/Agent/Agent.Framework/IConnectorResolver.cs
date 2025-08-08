// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Reasoning.Models;

namespace Agent.Framework;

public interface IConnectorResolver
{
    T GetConnectorFromSettings<T>(string connectorName) where T : DataConnectorDefinitionBase, new();
    
    List<DataConnectorBasicInfo> GetAllDataConnectors();
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
    /// Managed identity resource ID for authentication
    /// </summary>
    public string Identity { get; set; } = string.Empty;
}
