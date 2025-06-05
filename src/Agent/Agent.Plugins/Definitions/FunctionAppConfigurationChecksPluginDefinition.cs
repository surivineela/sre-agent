using System.ComponentModel;
using Agent.Framework;

namespace Agent.Plugins.Definitions
{

    /// <summary>
    /// Definition for the Function App Configuration Checks Plugin
    /// </summary>
    [AgentToolPlugin]
    public class FunctionAppConfigurationChecksPluginDefinition
    {
        private readonly IFunctionAppConfigurationChecksPlugin _functionAppConfigurationChecksPlugin;

        /// <summary>
        /// Constructor for FunctionAppConfigurationChecksPluginDefinition
        /// </summary>
        /// <param name="functionAppConfigurationChecksPlugin">The Function App Configuration Checks Plugin implementation</param>
        public FunctionAppConfigurationChecksPluginDefinition(IFunctionAppConfigurationChecksPlugin functionAppConfigurationChecksPlugin)
        {
            _functionAppConfigurationChecksPlugin = functionAppConfigurationChecksPlugin;
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
            return await _functionAppConfigurationChecksPlugin.GetFunctionAppConfigurationChecks(resourceId, startTime, endTime);
        }
    }
}
