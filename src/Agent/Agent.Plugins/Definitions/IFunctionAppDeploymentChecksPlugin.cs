using System;
using System.Threading.Tasks;

namespace Agent.Plugins.Definitions;

/// <summary>
/// Plugin for diagnosing and analyzing Function App deployment information
/// </summary>
public interface IFunctionAppDeploymentChecksPlugin
{
    /// <summary>
    /// Gets the thread ID for the plugin
    /// </summary>
    Guid? ThreadId { get; set; }

    /// <summary>
    /// Gets Function App deployment information for a Function App
    /// </summary>
    /// <param name="resourceId">The Azure resource ID of the Function App</param>
    /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
    /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
    /// <returns>A summary of function app deployment information</returns>
    Task<string> GetFunctionAppDeploymentChecks(string resourceId, DateTime? startTime = null, DateTime? endTime = null);
    
    /// <summary>
    /// Gets Function App deployment history for a Function App
    /// </summary>
    /// <param name="resourceId">The Azure resource ID of the Function App</param>
    /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
    /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
    /// <returns>A detailed history of function app deployments</returns>
    Task<string> GetFunctionAppDeploymentHistory(string resourceId, DateTime? startTime = null, DateTime? endTime = null);
    
    /// <summary>
    /// Checks if a resource is a Function App by verifying its 'kind' property contains 'functionapp'
    /// </summary>
    /// <param name="resourceId">The Azure resource ID to check</param>
    /// <returns>True if the resource is a Function App, false otherwise</returns>
    Task<bool> IsFunctionApp(string resourceId);
}
