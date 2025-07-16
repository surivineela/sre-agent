// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Plugins.Models;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Core.Models;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(Category = ToolCategories.AzureOperation)]
    public class FunctionAppsPluginDefinition
    {
        private readonly IFunctionAppsPlugin _functionAppPlugin;

        public FunctionAppsPluginDefinition(IFunctionAppsPlugin functionAppsPlugin)
        {
            _functionAppPlugin = functionAppsPlugin;
        }

        [Description("PREFERRED METHOD FOR FUNCTION APPS: Lists all Azure Function Apps in the specified subscription. " +
            "Returns detailed FunctionAppDescriptor objects containing resource ID, name, kind, location, SKU, state, resource group, and runtime details. " +
            "This is the most direct and efficient way to get Function App information. Use this instead of generic resource search methods. " +
            "Returns an empty list if no Function Apps are found or if the subscription doesn't exist.")]
        public async Task<IReadOnlyList<FunctionAppDescriptor>> ListFunctionAppsAsync(
            [Description("The Azure subscription ID to query for Function Apps.")] Guid subscriptionId)
        {
            return await _functionAppPlugin.ListFunctionAppsAsync(subscriptionId);
        }

        [Description("PREFERRED METHOD FOR FUNCTION APP DETAILS: Gets detailed information about a specific Azure Function App by its resource ID. " +
            "Returns a FunctionAppDescriptor with resource ID, name, kind, location, SKU, state, resource group, and runtime details. " +
            "Always use this specialized method for Function Apps instead of generic resource search functions for more complete and accurate information.")]
        public async Task<FunctionAppDescriptor> GetFunctionAppInfoAsync(
            [Description("The full Azure resource ID of the Function App to retrieve information for.")] string resourceId)
        {
            return await _functionAppPlugin.GetFunctionAppInfoAsync(resourceId);
        }
    }
}
