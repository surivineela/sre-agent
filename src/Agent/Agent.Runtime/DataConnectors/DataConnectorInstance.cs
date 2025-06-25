// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Configuration;

namespace Agent.Runtime.DataConnectors;

/// <summary>
/// Used internally to pass the data connector and its settings to the data connector service.
/// </summary>
/// <param name="DataConnector"></param>
/// <param name="Settings"></param>
[EditorBrowsable(EditorBrowsableState.Never)]
public record DataConnectorInstance(IDataConnector DataConnector, DataConnectorSettings Settings);
