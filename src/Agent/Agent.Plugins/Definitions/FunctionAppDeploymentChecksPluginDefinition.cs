using System.ComponentModel;
using Agent.Framework;

namespace Agent.Plugins.Definitions
{

    /// <summary>
    /// Definition for the Function App Deployment Checks Plugin
    /// </summary>
    [AgentToolPlugin]
    public class FunctionAppDeploymentChecksPluginDefinition
    {
        private readonly IFunctionAppDeploymentChecksPlugin _functionAppDeploymentChecksPlugin;

        /// <summary>
        /// Constructor for FunctionAppDeploymentChecksPluginDefinition
        /// </summary>
        /// <param name="functionAppDeploymentChecksPlugin">The Function App Deployment Checks Plugin implementation</param>
        public FunctionAppDeploymentChecksPluginDefinition(IFunctionAppDeploymentChecksPlugin functionAppDeploymentChecksPlugin)
        {
            _functionAppDeploymentChecksPlugin = functionAppDeploymentChecksPlugin;
        }

        /// <summary>
        /// Gets Function App deployment information for a Function App
        /// </summary>
        [Description("Gets Function App deployment information to identify potential deployment issues. " +
                    "Analyzes deployment history, source control information, deployment methods, and other deployment-related metrics. " +
                    "Returns detailed analysis with potential deployment issues and recommendations.")]
        public async Task<string> GetFunctionAppDeploymentChecks(
            [Description("The full Azure resource ID of the Function App to check deployments for.")] string resourceId,
            [Description("Optional start time for the query (defaults to 1 hour ago)")] DateTime? startTime = null,
            [Description("Optional end time for the query (defaults to current time minus 15 minutes)")] DateTime? endTime = null)
        {
            return await _functionAppDeploymentChecksPlugin.GetFunctionAppDeploymentChecks(resourceId, startTime, endTime);
        }

        /// <summary>
        /// Gets Function App deployment history for a Function App
        /// </summary>
        [Description("Gets detailed Function App deployment history to track all deployment activities. " +
                    "Retrieves chronological deployment records, including deployment source, trigger, status, and timestamps. " +
                    "Returns comprehensive deployment timeline with success/failure information.")]
        public async Task<string> GetFunctionAppDeploymentHistory(
            [Description("The full Azure resource ID of the Function App to retrieve deployment history for.")] string resourceId,
            [Description("Optional start time for the query (defaults to 1 hour ago)")] DateTime? startTime = null,
            [Description("Optional end time for the query (defaults to current time minus 15 minutes)")] DateTime? endTime = null)
        {
            return await _functionAppDeploymentChecksPlugin.GetFunctionAppDeploymentHistory(resourceId, startTime, endTime);
        }
    }
}
