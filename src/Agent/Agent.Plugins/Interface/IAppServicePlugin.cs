// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Models;

namespace Agent.Plugins.Interface;
public interface IAppServicePlugin
{
    Task<IReadOnlyList<AppServiceDescriptor>> ListAppServicesAsync(Guid subscriptionId);

    Task<AppServiceDescriptor> GetAppServiceInfoAsync(string resourceId);
}
