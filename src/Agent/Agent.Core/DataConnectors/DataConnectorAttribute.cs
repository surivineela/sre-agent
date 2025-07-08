// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace Agent.Core.DataConnectors;

public class DataConnectorAttribute : Attribute
{
    [SetsRequiredMembers]
    public DataConnectorAttribute(string type)
    {
        Type = type;
    }

    /// <summary>
    /// The name of the data connector. This should match the DataConnectorType in DataConnectorSettings.
    /// </summary>
    public required string Type { get; init; }
}
