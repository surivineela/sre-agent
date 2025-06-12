// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Models;

namespace Agent.Plugins.Interface;
public interface IFunctionAppsPlugin
{
    Task<IReadOnlyList<FunctionAppDescriptor>> ListFunctionAppsAsync(Guid subscriptionId);

    Task<FunctionAppDescriptor> GetFunctionAppInfoAsync(string resourceId);
}
