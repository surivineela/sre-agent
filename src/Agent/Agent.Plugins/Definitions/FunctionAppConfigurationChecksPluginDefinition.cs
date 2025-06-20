// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;

namespace Agent.Plugins.Definitions
{

    /// <summary>
    /// Definition for the Function App Configuration Checks Plugin
    /// </summary>
    [AgentToolPlugin]
    public class FunctionAppConfigurationChecksPluginDefinition
    {
        private readonly IFunctionAppConfigurationChecksPlugin _configChecksPlugin;

        /// <summary>
        /// Constructor for FunctionAppConfigurationChecksPluginDefinition
        /// </summary>
        /// <param name="configChecksPlugin">The Function App Configuration Checks Plugin implementation</param>
        public FunctionAppConfigurationChecksPluginDefinition(IFunctionAppConfigurationChecksPlugin configChecksPlugin)
        {
            _configChecksPlugin = configChecksPlugin;
        }

        /// <summary>
        /// Gets Function App configuration checks for a Function App
        /// </summary>
        [Description("Gets Function App configuration checks to identify potential issues in the Function App configuration. " +
                    "Analyzes settings like runtime version, extension version, platform, and other configuration values. " +
                    "Returns detailed analysis with potential issues and recommendations for optimization.")]
        public async Task<string> GetFunctionAppConfigurationChecks(
            [Description("The full Azure resource ID of the Function App to check configuration for.")] string resourceId,
            [Description("Optional start time for the query (defaults to 1 hour ago)")] DateTime? startTime = null,
            [Description("Optional end time for the query (defaults to current time minus 15 minutes)")] DateTime? endTime = null)
        {
            return await _configChecksPlugin.GetFunctionAppConfigurationChecks(resourceId, startTime, endTime);
        }

        /// <summary>
        /// Gets Event Grid subscriptions associated with a storage account used by a Function App
        /// </summary>
        [Description("Gets Event Grid subscriptions associated with a storage account used by a Function App. " +
                    "Returns detailed information about each subscription including endpoint, filter criteria, and retry policy.")]
        public async Task<IReadOnlyList<EventGridSubscriptionInfo>> GetEventGridSubscriptionsAsync(
            [Description("The resource ID of the storage account to check for Event Grid subscriptions.")] string storageAccountResourceId)
        {
            return await _configChecksPlugin.GetEventGridSubscriptionsAsync(storageAccountResourceId);
        }

    }
}
