using System.ComponentModel;
using Agent.Plugins.Models;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin]
    public class APIManagementPluginDefinition
    {
        private IAPIManagementPlugin _apiManagementPlugin;

        public APIManagementPluginDefinition(IAPIManagementPlugin apiManagementPlugin)
        {
            _apiManagementPlugin = apiManagementPlugin;
        }

        [Description(
               "PREFERRED METHOD FOR API MANAGEMENT DETAILS: Gets detailed information about a specific Azure API Management instance by its resource ID. " +
               "Returns an APIManagementDescriptor with resource ID, name, state, and environment details. " +
               "Always use this specialized method for API Management instances instead of generic resource search functions for more complete and accurate information. " +
               "For metrics and usage information (such as requests, throughput, errors, cost, etc.), format the output in markdown tabular format.")]
        public async Task<APIManagementDescriptor> GetAPIManagementInfoAsync(
               [Description(
                "The full Azure resource ID of the API Management instance (format: /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.ApiManagement/service/{serviceName}).")]
            string resourceId)
        {
            return await _apiManagementPlugin.GetAPIManagementInfoAsync(resourceId);
        }

        [Description(
                "PREFERRED METHOD FOR API MANAGEMENT RESOURCES: Lists all Azure API Management resources in the specified subscription. " +
               "Returns an APIManagementDescriptor with resource ID, name, state, and environment details. " +
                "This is the most direct and efficient way to get API Management resource information - use this instead of generic resource search methods. Returns an empty list if no API Management resources are found.")]
        public async Task<IReadOnlyList<APIManagementDescriptor>> ListAPIManagementAsync(
                [Description("The subscription ID (GUID) to scan for API Management resources.")]
            Guid subscriptionId)
        {
            return await _apiManagementPlugin.ListAPIManagementAsync(subscriptionId);
        }

        [Description(
                "Retrieves recent failed requests (non-successful) from an Azure API Management instance using connected Application Insights. " +
                "Supports optional filtering by status code and allows specifying how many results to return. " +
                "If no time range is provided, it defaults to the past 5 days up to 15 minutes ago.")]
        public async Task<string> GetAPIMErrorLogsAsync(
                [Description("The full Azure resource ID of the API Management instance (format: /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.ApiManagement/service/{serviceName}).")] string apiManagementResourceId,
                [Description("Optional. Filter by HTTP status code (e.g., 500, 404). If not provided, all failed responses are included.")] string statusCode = null,
                [Description("Optional. Number of log entries to retrieve. Defaults to 6.")] int top = 6,
                [Description("Optional. Number of days ago to start the range (e.g., 5 for 5 days ago). Defaults to 5.")] int startDaysAgo = 5,
                [Description("Optional. Number of days ago to end the range (e.g., 0 for now, 1 for 1 day ago). Defaults to 0.")] int endDaysAgo = 0)
        {
            DateTime startTime = DateTime.UtcNow.AddDays(-startDaysAgo);
            DateTime endTime = DateTime.UtcNow.AddDays(-endDaysAgo);
            if (startTime >= endTime) { return $"Invalid time range: startDaysAgo ({startDaysAgo}) must be less than endDaysAgo ({endDaysAgo})."; }

            return await _apiManagementPlugin.GetAPIMErrorLogsAsync(apiManagementResourceId, startTime, endTime, statusCode, top);
        }

        [Description(
            "Retrieves the management activity (changes, deploymenents, admin actions) logs for a specified Azure API Management instance over the past 7 days. " +
            "Returns a markdown table with columns: Timestamp, Operation, Event, Status, URI, Caller. " +
            "This method queries Azure Monitor's management event logs for the resource. " +
            "Use this to audit changes, deployments, or administrative actions on the API Management instance. " +
            "If startTime and endTime are not provided, defaults to the past 2 days. Pass in the datetimes as parameters to override the default window."
        )]
        public async Task<string> GetAPIMActivityLogs(
            [Description("Full Azure resource ID of the API Management instance (e.g. /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.ApiManagement/service/{serviceName})")] string apiManagementResourceId,
            [Description("Optional. Number of days ago to start the range (e.g., 3 for 3 days ago). Defaults to 3.")] int startDaysAgo = 3,
            [Description("Optional. Number of days ago to end the range (e.g., 0 for now, 1 for 1 day ago). Defaults to 0.")] int endDaysAgo = 0)
        {
            DateTime startTime = DateTime.UtcNow.AddDays(-startDaysAgo);
            DateTime endTime = DateTime.UtcNow.AddDays(-endDaysAgo);
            if (startTime >= endTime) { return $"Invalid time range: startDaysAgo ({startDaysAgo}) must be less than endDaysAgo ({endDaysAgo})."; }

            return await _apiManagementPlugin.GetAPIMActivityLogs(apiManagementResourceId, startTime, endTime);
        }
    }
}
