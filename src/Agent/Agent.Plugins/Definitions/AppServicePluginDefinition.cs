// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;

namespace Agent.Plugins.Definitions
{
    public class AppServicePluginDefinition
    {
        private readonly IAppServicePlugin _appServicePlugin;
        public AppServicePluginDefinition(IAppServicePlugin appServicePlugin)
        {
            _appServicePlugin = appServicePlugin;
        }

        [Description("PREFERRED METHOD FOR APP SERVICES: Lists all Azure App Services (web apps) in the specified subscription. " +
            "Returns detailed AppServiceDescriptor objects containing resource ID, name, kind, location, SKU, state, and resource group. " +
            "This is the most direct and efficient way to get App Service information. Use this instead of generic resource search methods. " +
            "Returns an empty list if no App Services are found or if the subscription doesn't exist.")]
        public async Task<IReadOnlyList<AppServiceDescriptor>> ListAppServicesAsync(Guid subscriptionId)
        {
            return await _appServicePlugin.ListAppServicesAsync(subscriptionId);
        }

        [Description("PREFERRED METHOD FOR APP SERVICE DETAILS: Gets detailed information about a specific Azure App Service by its resource ID. " +
            "Returns an AppServiceDescriptor with resource ID, name, kind, location, SKU, state, and resource group. " +
            "Always use this specialized method for App Services instead of generic resource search functions for more complete and accurate information.")]
        public async Task<AppServiceDescriptor> GetAppServiceInfoAsync(string resourceId)
        {
            return await _appServicePlugin.GetAppServiceInfoAsync(resourceId);
        }
    }
}
