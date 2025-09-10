// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Plugins.Models;
using Agent.Plugins.Interface;
using Agent.Core.Models;
using Agent.Framework;

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
        [AgentTool(ToolMode.Auto)]
        public async Task<IReadOnlyList<FunctionAppDescriptor>> ListFunctionAppsAsync(
            [Description("The Azure subscription ID to query for Function Apps.")] Guid subscriptionId)
        {
            return await _functionAppPlugin.ListFunctionAppsAsync(subscriptionId);
        }

        [Description("PREFERRED METHOD FOR FUNCTION APP DETAILS: Gets detailed information about a specific Azure Function App by its resource ID. " +
            "Returns a FunctionAppDescriptor with resource ID, name, kind, location, SKU, state, resource group, and runtime details. " +
            "Always use this specialized method for Function Apps instead of generic resource search functions for more complete and accurate information.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<FunctionAppDescriptor?> GetFunctionAppInfoAsync(
            [Description("The full Azure resource ID of the Function App to retrieve information for.")] string resourceId)
        {
            return await _functionAppPlugin.GetFunctionAppInfoAsync(resourceId);
        }

        [Description("Gets all deployment slots for a specific Azure Function App. " +
            "First checks if the Function App's SKU supports deployment slots (Standard, Premium, or Isolated tiers only). " +
            "Returns a list of resource IDs for all deployment slots, or an empty list if no slots exist or the SKU doesn't support slots. " +
            "Note: Consumption, Basic, Free, and Shared plans do not support deployment slots.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<List<string>> GetFunctionAppDeploymentSlotsAsync(
            [Description("The full Azure resource ID of the Function App to get deployment slots for.")] string resourceId)
        {
            return await _functionAppPlugin.GetFunctionAppDeploymentSlotsAsync(resourceId);
        }

        [Description("Triggers a TimerTrigger Azure Function manually. " +
            "Only supports functions that use TimerTrigger bindings (scheduled functions). " +
            "Automatically retrieves the master key from Azure ARM API and validates that the specified function is indeed a TimerTrigger. " +
            "Returns detailed execution results including success status, response content, and duration. " +
            "Use this to manually invoke scheduled functions outside their normal schedule or test TimerTrigger functions.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<FunctionTriggerResponse> TriggerTimerFunctionAsync(
            [Description("The full Azure resource ID of the Function App containing the TimerTrigger function to trigger.")] string functionAppResourceId,
            [Description("The name of the TimerTrigger function to trigger (case-sensitive).")] string functionName)
        {
            return await _functionAppPlugin.TriggerTimerFunctionAsync(functionAppResourceId, functionName);
        }
    }
}
