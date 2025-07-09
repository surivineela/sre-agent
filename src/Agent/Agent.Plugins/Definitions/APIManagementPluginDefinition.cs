using System.ComponentModel;
using Agent.Core.Attributes;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using static Agent.Plugins.Helpers.APIManagementHelper;

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

        #region Basic API Management Information

        [Description(
            "PREFERRED METHOD FOR API MANAGEMENT DETAILS: Gets detailed information about a specific Azure API Management instance by its resource ID. " +
            "Returns an APIManagementDescriptor with the following properties: " +
            "ResourceId, Name, Type, Location, ResourceGroup, PublisherEmail, PublisherName, SkuName, VirtualNetworkConfiguration, GatewayUri, GatewayRegionalUri, " +
            "HostnameConfigurations, PublicIPAddresses, PrivateIPAddresses, VirtualNetworkType, PublicNetworkAccess, CustomProperties, Certificates, EnableClientCertificate, " +
            "ProvisioningState, PlatformVersion, DeveloperPortalUri, DeveloperPortalStatus, PortalUri, ScmUri, ManagementApiUri, AppHealthInformation and CreatedAtUtc. " +
            "Always use this specialized method for API Management instances instead of generic resource search functions for more complete and accurate information. " +
            "For metrics and usage information (such as requests, throughput, errors, cost, etc.), format the output in markdown tabular format.")]
        public async Task<APIManagementDescriptor?> GetAPIManagementInfoAsync(
            [Description("The full Azure resource ID of the API Management instance (format: /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.ApiManagement/service/{serviceName}).")] string resourceId)
        {
            return await _apiManagementPlugin.GetAPIManagementInfoAsync(resourceId);
        }

        [Description(
            "PREFERRED METHOD FOR API MANAGEMENT RESOURCES: Lists all Azure API Management resources in the specified subscription. " +
            "Returns a string of APIManagementDescriptors, each with the following properties: " +
            "ResourceId, Name, Type, Location, ResourceGroup, and PublisherEmail. " +
            "These exact properties are returned to the customer for each API Management resource. " +
            "This is the most direct and efficient way to get API Management resource information - use this instead of generic resource search methods. Returns an empty list if no API Management resources are found.")]
        public async Task<List<APIManagementDescriptor>> ListAPIManagementAsync(
            [Description("The subscription ID (GUID) to scan for API Management resources.")] Guid subscriptionId)
        {
            return await _apiManagementPlugin.ListAPIManagementAsync(subscriptionId);
        }

        #endregion

        #region API Error Logs and Activity - Diagnostics

        // NOTE: The reason these functions take relative time parameters (e.g., startDaysAgo, endDaysAgo, lookbackHours) is because the agent does not have a consistent definition of "current time" and often defaults to UTC DateTime values based on its training data. 
        // Using relative time ensures more predictable and accurate results regardless of the agent's runtime environment or time zone.

        [Description(
            "Retrieves recent failed requests (non-successful) from an Azure API Management instance using connected Application Insights. " +
            "Supports optional filtering by status code and allows specifying how many results to return. " +
            "You can specify a time window using startHoursAgo/endHoursAgo (relative to now, in hours). " +
            "If neither is provided, defaults to the past 24 hours up to 0 hours ago.")]
        public async Task<string> GetAPIMErrorLogsAsync(
            [Description("The full Azure resource ID of the API Management instance (format: /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.ApiManagement/service/{serviceName}).")] string apiManagementResourceId,
            [Description("Optional. Filter by HTTP status code (e.g., 500, 404). If not provided, all failed responses are included.")] string statusCode = null,
            [Description("Optional. Number of log entries to retrieve. Defaults to 6.")] int top = 6,
            [Description("Optional. Number of hours ago to start the range (e.g., 24 for 24 hours ago). Defaults to 24.")] int startHoursAgo = 24,
            [Description("Optional. Number of hours ago to end the range (e.g., 0 for now, 1 for 1 hour ago). Defaults to 0.")] int endHoursAgo = 0)
        {
            DateTime startTime = DateTime.UtcNow.AddHours(-startHoursAgo);
            DateTime endTime = DateTime.UtcNow.AddHours(-endHoursAgo);
            if (startTime >= endTime) { return $"Invalid time range: startHoursAgo ({startHoursAgo}) must be greater than endHoursAgo ({endHoursAgo})."; }

            return await _apiManagementPlugin.GetAPIMErrorLogsAsync(apiManagementResourceId, startTime, endTime, statusCode, top);
        }

        [Description(
            "Retrieves the management activity (changes, deploymenents, admin actions) logs for a specified Azure API Management instance over the past 24 hours. " +
            "Returns a markdown table with columns: Timestamp, Operation, Event, Status, URI, Caller. " +
            "This method queries Azure Monitor's management event logs for the resource. " +
            "Use this to audit changes, deployments, or administrative actions on the API Management instance. " +
            "If startHoursAgo and endHoursAgo are not provided, defaults to the past 24 hours. Pass in the datetimes as parameters to override the default window."
        )]
        public async Task<string> GetAPIMActivityLogsAsync(
            [Description("Full Azure resource ID of the API Management instance (e.g. /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.ApiManagement/service/{serviceName})")] string apiManagementResourceId,
            [Description("Optional. Number of hours ago to start the range (e.g., 24 for 24 hours ago). Defaults to 24.")] int startHoursAgo = 24,
            [Description("Optional. Number of hours ago to end the range (e.g., 0 for now, 1 for 1 hour ago). Defaults to 0.")] int endHoursAgo = 0)
        {
            DateTime startTime = DateTime.UtcNow.AddHours(-startHoursAgo);
            DateTime endTime = DateTime.UtcNow.AddHours(-endHoursAgo);
            if (startTime >= endTime) { return $"Invalid time range: startHoursAgo ({startHoursAgo}) must be greater than endHoursAgo ({endHoursAgo})."; }

            return await _apiManagementPlugin.GetAPIMActivityLogsAsync(apiManagementResourceId, startTime, endTime);
        }

        [Description(
            "Calculates the failure rate for each API operation over a specified time range using relative hours. " +
            "startHoursAgo and endHoursAgo are optional integers relative to now (e.g., 24 and 0 means from 24 hours ago to now). " +
            "If not provided, startHoursAgo defaults to 24 and endHoursAgo defaults to 0. " +
            "Returns a markdown table with columns: ApiId, OperationId, ResponseCode, LastErrorReason, TotalCount, FailedCount, FailureRatePercent.")]
        public async Task<string> GetAPIMFailureRateByApiOperationAsync(
            [Description("Full Azure resource ID of the API Management instance (e.g. /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.ApiManagement/service/{serviceName})")] string apiManagementResourceId,
            [Description("Optional. Number of hours ago to start the range (e.g., 24 for 24 hours ago). Defaults to 24.")] int startHoursAgo = 24,
            [Description("Optional. Number of hours ago to end the range (e.g., 0 for now, 1 for 1 hour ago). Defaults to 0.")] int endHoursAgo = 0)
        {
            DateTime startTime = DateTime.UtcNow.AddHours(-startHoursAgo);
            DateTime endTime = DateTime.UtcNow.AddHours(-endHoursAgo);
            if (startTime >= endTime) { return $"Invalid time range: startHoursAgo ({startHoursAgo}) must be greater than endHoursAgo ({endHoursAgo})."; }

            return await _apiManagementPlugin.GetAPIMFailureRateByApiOperationAsync(apiManagementResourceId, startTime, endTime);
        }

        [Description(
            "Retrieves the most recent failed requests (up to a specified limit) with full request/response details. " +
            "Defaults to the past 24 hours and top 10 results if no parameters are provided. " +
            "Returns a markdown table with columns: TimeGenerated, CorrelationId, ApiId, OperationId, Url, Method, CallerIpAddress, ResponseCode, LastErrorReason, LastErrorMessage, RequestSize, ResponseSize, RequestHeaders, ResponseHeaders, RequestBody, ResponseBody.")]
        public async Task<string> GetAPIMRecentFailedRequestsAsync(
            [Description("Full Azure resource ID of the API Management instance (e.g. /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.ApiManagement/service/{serviceName})")] string apiManagementResourceId,
            [Description("Optional. Hours to look back to; defaults to 24 hours if omitted.")] int lookbackHours = 24,
            [Description("Optional. Maximum number of failures to return; defaults to 10.")] int topN = 10)
        {
            TimeSpan lookback = TimeSpan.FromHours(lookbackHours);

            return await _apiManagementPlugin.GetAPIMRecentFailedRequestsAsync(apiManagementResourceId, lookback, topN);
        }

        #endregion

        #region APIs, Operations, and Policies - Diagnostics

        [Description(
            "Retrieves the list of APIs defined in the specified Azure API Management instance. " +
            "Returns a markdown table with columns: ApiId, Name, Description, Path, Protocols, ServiceUrl. " +
            "This method queries the API Management service for its defined APIs.")]
        public async Task<List<APIManagementApiDescriptor>> GetAPIMApisAsync(
            [Description("Full Azure resource ID of the API Management instance (e.g. /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.ApiManagement/service/{serviceName})")] string apiManagementResourceId,
            [Description("Optional name of the workspace within the API Management instance.")] string? workspaceName = null)
        {
            return await _apiManagementPlugin.GetAPIMApisAsync(apiManagementResourceId, workspaceName);
        }

        [Description(
            "Retrieves detailed information about a specific API in the Azure API Management instance by its name. " +
            "Returns an APIManagementApiDescriptor with properties like Id, Name, Type, and detailed properties including display name, revision, description, subscription requirements, service URL, backend ID, path, protocols, authentication settings, and subscription key parameter names. " +
            "This method queries the API Management service for the specified API.")]
        public async Task<APIManagementApiDescriptor> GetAPIDetailsByNameAsync(
            [Description("Full Azure resource ID of the API Management instance (e.g. /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.ApiManagement/service/{serviceName})")] string apiManagementResourceId,
            [Description("The name of the API to retrieve details for.")] string apiName,
            [Description("Optional name of the workspace within the API Management instance.")] string? workspaceName = null)
        {
            return await _apiManagementPlugin.GetAPIDetailsByNameAsync(apiManagementResourceId, apiName, workspaceName);
        }

        [Description(
            "Retrieves the list of operations for a specific API in the Azure API Management instance. " +
            "Returns a markdown table with columns: OperationId, Name, Description, Method, UrlTemplate, ResponseCodes. " +
            "This method queries the API Management service for operations defined under the specified API.")]
        public async Task<List<APIManagementApiOperationSummary>> GetAPIOperationsByApiAsync(
            [Description("Full Azure resource ID of the API Management instance (e.g. /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.ApiManagement/service/{serviceName})")] string apiManagementResourceId,
            [Description("The name of the API to retrieve operations for.")] string apiName,
            [Description("Optional name of the workspace within the API Management instance.")] string? workspaceName = null)

        {
            return await _apiManagementPlugin.GetAPIOperationsByApiAsync(apiManagementResourceId, apiName, workspaceName);
        }

        [Description(
            "Retrieves detailed information about a specific operation in an API within the Azure API Management instance. " +
            "Returns a markdown table with columns: OperationId, Name, Policies, Method, Responses, Properties, etc. " +
            "This method queries the API Management service for detailed operation information.")]
        public async Task<APIManagementApiOperationDescriptor> GetAPIOperationDetailedInfoAsync(
            [Description("Full Azure resource ID of the API Management instance (e.g. /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.ApiManagement/service/{serviceName})")] string apiManagementResourceId,
            [Description("The name of the API to retrieve operations for.")] string apiName,
            [Description("The name of the operation to retrieve detailed information for.")] string operationName,
            [Description("Optional name of the workspace within the API Management instance.")] string? workspaceName = null)
        {
            return await _apiManagementPlugin.GetAPIOperationDetailedInfoAsync(apiManagementResourceId, apiName, operationName, workspaceName);
        }
        #endregion

        #region Virtual Networking - Diagnostics

        [Description(
            "Retrieves the full virtual network configuration for a specified Azure API Management instance. " +
            "Returns details about the associated VNet including address space, subnets, NSG associations, DNS settings, and private endpoint policies. " +
            "Use this to understand how the APIM instance is integrated into its virtual network and to verify correct setup.")]
        public async Task<VirtualNetworkDetails?> GetVNetConfigurationForApiManagementAsync(
            [Description("Full Azure resource ID of the API Management instance (e.g. /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.ApiManagement/service/{serviceName})")] string apimResourceId)
        {
            return await _apiManagementPlugin.GetVNetConfigurationForApiManagementAsync(apimResourceId);
        }

        [Description(
            "Runs a diagnostic check potential for virtual network connectivity issues for a specified Azure API Management instance within a given time window. " +
            "Returns information summarizing detected issues, affected subnets, error types, and recommended actions. " +
            "Use this to troubleshoot APIM instances that are experiencing connectivity or integration problems with their virtual network."
        )]
        public async Task<string> CheckForVirtualNetworkIssuesAsync(
            [Description("Full Azure resource ID of the API Management instance (e.g. /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.ApiManagement/service/{serviceName})")] string apimResourceId,
            [Description("Optional. Number of hours ago to start the range (e.g., 24 for 24 hours ago). Defaults to 24.")] int startHoursAgo = 24,
            [Description("Optional. Number of hours ago to end the range (e.g., 0 for now, 1 for 1 hour ago). Defaults to 0.")] int endHoursAgo = 0)
        {
            DateTime startTime = DateTime.UtcNow.AddHours(-startHoursAgo);
            DateTime endTime = DateTime.UtcNow.AddHours(-endHoursAgo);
            if (startTime >= endTime) { return $"Invalid time range: startHoursAgo ({startHoursAgo}) must be greater than endHoursAgo ({endHoursAgo})."; }

            return await _apiManagementPlugin.CheckForVirtualNetworkIssuesAsync(apimResourceId, startTime, endTime);
        }

        [Description(
            "Retrieves Network Security Group (NSG) rules associated with an API Management instance. " +
            "Returns a dictionary or formatted string of NSG rule names and their properties. " +
            "If 'getCustomOnly' is set to true, only custom (non-system-managed) rules will be returned. " +
            "This is useful for reviewing the network security posture or identifying firewall rules that may impact connectivity.")]
        public async Task<List<NSGRuleDetails>> GetNSGRulesForApiManagementAsync(
            [Description("Full Azure resource ID of the API Management instance (e.g. /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.ApiManagement/service/{serviceName})")] string resourceId,
            [Description("Optional flag to return only custom (non-default) rules. Defaults to false. Set to true to exclude system-managed rules.")] bool getCustomOnly = false)
        {
            return await _apiManagementPlugin.GetNSGRulesForApiManagementAsync(resourceId, getCustomOnly);
        }

        [Description(
            "Retrieves Network Security Group (NSG) administrative activity logs for a specified Azure API Management instance within a given time window. " +
            "Returns a markdown table with columns: TimeGenerated, OperationName, Status, Caller, SourceAddress, DestinationAddress, Protocol, Port, RuleName, and Description. " +
            "Specify the time window using startDaysAgo and endDaysAgo (relative to now, in days). Defaults to the past 1 day if not provided."
        )]
        public async Task<string> GetNSGActivityLogsAsync(
            [Description("Full Azure resource ID of the API Management instance (e.g. /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.ApiManagement/service/{serviceName})")] string apimResourceId,
            [Description("Optional. Number of faw log entries to look into. Defaults to 40.")] int topNAzureLogs = 40,
            [Description("Optional. Maximum number of relevant NSG-related findings to include in the report. Defaults to 10.")] int maxFindings = 10)
        {
            return await _apiManagementPlugin.GetNSGActivityLogsAsync(apimResourceId, topNAzureLogs, maxFindings);
        }

        #endregion

        #region Virtual Network - Remediation 

        [RequiresApproval("Your approval is required before I remove a Network Security Group (NSG) rule. Please confirm to proceed.")]
        [Description(
            "Removes a specified Network Security Group (NSG) rule from an Azure NSG resource. " +
            "This is typically used to unblock connectivity for an API Management instance or related resources. " +
            "Returns true if the rule was successfully removed, false otherwise. " +
            "Use this to automate the removal of firewall or network restrictions that may be impacting APIM connectivity. " +
            "MANDATORY: Before using this method, you must explain to the user what is going to be done and any possible consequences, and inform them that the action will be performed on their behalf."
        )]
        public async Task<bool> APIMRemoveNSGRuleAsync(
            [Description("The full Azure resource ID of the Network Security Group (NSG) (e.g., /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.Network/networkSecurityGroups/{nsgName})")] string nsgResourceId,
            [Description("The name of the NSG rule to remove.")] string ruleName)
        {
            return await _apiManagementPlugin.APIMRemoveNSGRuleAsync(nsgResourceId, ruleName);
        }

        [RequiresApproval("Your approval is required before I modify a Network Security Group (NSG) rule. Please confirm to proceed.")]
        [Description(
            "Modifies properties of an existing Network Security Group (NSG) rule in an Azure NSG resource. " +
            "Use this to update firewall rules to unblock or adjust connectivity for an API Management instance or related resources. " +
            "Only the parameters provided (non-null) will be updated; all others will remain unchanged. " +
            "Returns true if the rule was successfully modified, false otherwise. " +
            "MANDATORY: Before using this method, you must explain to the user what is going to be done and any possible consequences, and inform them that the action will be performed on their behalf."
        )]
        public async Task<bool> APIMModifyNSGRuleAsync(
            [Description("The full Azure resource ID of the Network Security Group (NSG) (e.g., /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.Network/networkSecurityGroups/{nsgName})")] string nsgResourceId,
            [Description("The name of the NSG rule to modify.")] string ruleName,
            [Description("New priority for the rule (e.g., '100', '200'). Must be between 100 and 4096. Leave null to keep existing value.")] string? priority = null,
            [Description("New access type: 'Allow' or 'Deny'. Case-insensitive. Leave null to keep existing value.")] string? access = null,
            [Description("New direction: 'Inbound' or 'Outbound'. Case-insensitive. Leave null to keep existing value.")] string? direction = null,
            [Description("New protocol: 'Tcp', 'Udp', or '*' for any. Case-insensitive. Leave null to keep existing value.")] string? protocol = null,
            [Description("New source port range (e.g., '80', '*'). Leave null to keep existing value.")] string? sourcePortRange = null,
            [Description("New destination port range (e.g., '443', '8080', '*'). Leave null to keep existing value.")] string? destinationPortRange = null,
            [Description("New source address prefix (e.g., '*', '10.0.0.0/24'). Leave null to keep existing value.")] string? sourceAddressPrefix = null,
            [Description("New destination address prefix (e.g., '*', '10.1.0.0/24'). Leave null to keep existing value.")] string? destinationAddressPrefix = null,
            [Description("New description for the rule. Leave null to keep existing value.")] string? description = null)
        {
            return await _apiManagementPlugin.APIMModifyNSGRuleAsync(nsgResourceId, ruleName, priority, access, direction, protocol, sourcePortRange, destinationPortRange, sourceAddressPrefix, destinationAddressPrefix, description);
        }

        #endregion
    }
}
