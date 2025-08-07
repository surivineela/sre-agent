// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Reasoning.Models;

namespace Agent.Framework;

public interface IConnectorResolver
{
    T GetConnectorFromSettings<T>(string connectorName) where T : DataConnectorDefinitionBase, new();
}
