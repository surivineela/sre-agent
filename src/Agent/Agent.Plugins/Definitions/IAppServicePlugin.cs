// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Definitions;
public interface IAppServicePlugin
{
    Task<IReadOnlyList<AppServiceDescriptor>> ListAppServicesAsync(Guid subscriptionId);

    Task<AppServiceDescriptor> GetAppServiceInfoAsync(string resourceId);
}
