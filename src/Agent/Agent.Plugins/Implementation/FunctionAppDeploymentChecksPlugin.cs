using Agent.Core.Helpers;
using Agent.Logging;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation
{

    /// <summary>
    /// Implementation of the Function App Deployment Checks Plugin
    /// </summary>
    public class FunctionAppDeploymentChecksPlugin : IFunctionAppDeploymentChecksPlugin
    {
        private readonly ILogger<FunctionAppDeploymentChecksPlugin> _logger;
        private readonly ArmHelper _armHelper;

        /// <summary>
        /// Gets or sets the thread ID
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Constructor for FunctionAppDeploymentChecksPlugin
        /// </summary>
        /// <param name="logger">Logger for the plugin</param>
        /// <param name="armHelper">ARM helper for interacting with Azure resources</param>
        public FunctionAppDeploymentChecksPlugin(
            ILogger<FunctionAppDeploymentChecksPlugin> logger,
            ArmHelper armHelper)
        {
            _logger = logger;
            _armHelper = armHelper;
        }

        /// <summary>
        /// Gets Function App deployment information for a Function App
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App</param>
        /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
        /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
        /// <returns>A summary of function app deployment information</returns>
        public async Task<string> GetFunctionAppDeploymentChecks(string resourceId, DateTime? startTime = null, DateTime? endTime = null)
        {
            try
            {
                _logger.LogInternalInformation("Getting Function App deployment information for {ResourceId}", resourceId);

                // Call GetDetectorResponseWithTime with the 'FunctionsDeploymentExternal' detector ID
                string result = await _armHelper.GetDetectorResponseWithTime(resourceId, "FunctionsDeploymentExternal", startTime, endTime);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting Function App deployment information for {ResourceId}", resourceId);
                throw;
            }
        }

        /// <summary>
        /// Gets Function App deployment history for a Function App
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App</param>
        /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
        /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
        /// <returns>A detailed history of function app deployments</returns>
        public async Task<string> GetFunctionAppDeploymentHistory(string resourceId, DateTime? startTime = null, DateTime? endTime = null)
        {
            try
            {
                _logger.LogInternalInformation("Getting Function App deployment history for {ResourceId}", resourceId);

                // Call GetDetectorResponseWithTime with the 'FunctionAppDeployed' detector ID
                string result = await _armHelper.GetDetectorResponseWithTime(resourceId, "FunctionAppDeployed", startTime, endTime);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting Function App deployment history for {ResourceId}", resourceId);
                throw;
            }
        }
    }
}
