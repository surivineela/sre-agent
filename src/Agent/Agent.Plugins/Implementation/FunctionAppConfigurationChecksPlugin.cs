using System;
using System.Threading.Tasks;
using Agent.Core.Helpers;
using Agent.Logging;
using Agent.Plugins.Definitions;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

/// <summary>
/// Implementation of the Function App Configuration Checks Plugin
/// </summary>
public class FunctionAppConfigurationChecksPlugin : IFunctionAppConfigurationChecksPlugin
{
    private readonly ILogger<FunctionAppConfigurationChecksPlugin> _logger;
    private readonly ArmHelper _armHelper;

    /// <summary>
    /// Gets or sets the thread ID
    /// </summary>
    public Guid? ThreadId { get; set; }

    /// <summary>
    /// Constructor for FunctionAppConfigurationChecksPlugin
    /// </summary>
    /// <param name="logger">Logger for the plugin</param>
    /// <param name="armHelper">ARM helper for interacting with Azure resources</param>
    public FunctionAppConfigurationChecksPlugin(
        ILogger<FunctionAppConfigurationChecksPlugin> logger,
        ArmHelper armHelper)
    {
        _logger = logger;
        _armHelper = armHelper;
    }

    /// <summary>
    /// Gets Function App configuration checks for a Function App
    /// </summary>
    /// <param name="resourceId">The Azure resource ID of the Function App</param>
    /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
    /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
    /// <returns>A summary of function app configuration checks</returns>
    public async Task<string> GetFunctionAppConfigurationChecks(string resourceId, DateTime? startTime = null, DateTime? endTime = null)
    {
        try
        {
            _logger.LogInternalInformation("Getting Function App configuration checks for {ResourceId}", resourceId);

            // Call GetAnalysisWithTime with the 'functionsettings' detector ID
            string result = await _armHelper.GetAnalysisWithTime(resourceId, "functionsettings", startTime, endTime);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting Function App configuration checks for {ResourceId}", resourceId);
            throw;
        }
    }

    /// <summary>
    /// Checks if a resource is a Function App by verifying its 'kind' property contains 'functionapp'
    /// </summary>
    /// <param name="resourceId">The Azure resource ID to check</param>
    /// <returns>True if the resource is a Function App, false otherwise</returns>
    public async Task<bool> IsFunctionApp(string resourceId)
    {
        try
        {
            _logger.LogInternalInformation("Checking if {ResourceId} is a Function App", resourceId);

            // Get the ARM resource as JSON
            string armResource = await _armHelper.GetArmResourceAsJsonAsync(resourceId);

            // Check if the resource contains 'functionapp' in its kind property
            return armResource.Contains("\"kind\"") && armResource.Contains("functionapp");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error checking if {ResourceId} is a Function App", resourceId);
            return false;
        }
    }
}
