// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Models;

namespace Agent.Plugins.Definitions;
public interface IFunctionAppsPlugin
{
    Task<IReadOnlyList<FunctionAppDescriptor>> ListFunctionAppsAsync(Guid subscriptionId);

    Task<FunctionAppDescriptor> GetFunctionAppInfoAsync(string resourceId);
}
