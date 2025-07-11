using Agent.Plugins.Models;
using static Agent.Plugins.Helpers.APIManagementHelper;
using Azure.ResourceManager.ApiManagement;
using Azure.ResourceManager.ApiManagement.Models;

namespace Agent.Plugins.Interface
{
    public interface IAPIManagementPlugin
    {
        Guid? ThreadId { get; set; }

        Task<List<APIManagementDescriptor>> ListAPIManagementAsync(Guid subscriptionId);

        Task<APIManagementDescriptor?> GetAPIManagementInfoAsync(string resourceId);

        Task<string> GetAPIMErrorLogsAsync(string apimInstanceResourceId, DateTime startTime, DateTime endTime, string statusCode, int top);

        Task<string> GetAPIMFailureRateByApiOperationAsync(string apiManagementResourceId, DateTime startTime, DateTime endTime);

        Task<string> GetAPIMRecentFailedRequestsAsync(string apiManagementResourceId, TimeSpan lookback, int topN);

        Task<string> GetAPIMActivityLogsAsync(string apimResourceId, DateTime startTime, DateTime endTime);

        Task<List<APIManagementApiDescriptor>> GetAPIMApisAsync(string apiManagementResourceId, string workspaceName);

        Task<APIManagementApiDescriptor> GetAPIDetailsByNameAsync(string apiManagementResourceId, string apiName, string workspaceName);

        Task<List<APIManagementApiOperationSummary>> GetAPIOperationsByApiAsync(string apiManagementResourceId, string apiName, string workspaceName);

        Task<APIManagementApiOperationDescriptor> GetAPIOperationDetailedInfoAsync(string apiManagementResourceId, string apiName, string operationName, string workspaceName);

        Task<VirtualNetworkDetails?> GetVNetConfigurationForApiManagementAsync(string apimResourceId);

        Task<string> CheckForVirtualNetworkIssuesAsync(string apimResourceId, DateTime issueStartTime, DateTime issueEndTime);

        Task<List<NSGRuleDetails>> GetNSGRulesForApiManagementAsync(string apimResourceId, bool getCustomOnly);

        Task<string> GetNSGActivityLogsAsync(string apimResourceId, int topNAzureLogs, int maxFindings);

        Task<bool> APIMRemoveNSGRuleAsync(string nsgResourceId, string ruleName);

        Task<bool> APIMModifyNSGRuleAsync(string nsgResourceId, string ruleName, string? priority = null, string? access = null, string? direction = null, string? protocol = null, string? sourcePortRange = null, string? destinationPortRange = null, string? sourceAddressPrefix = null, string? destinationAddressPrefix = null, string? description = null);

        Task<ApiPolicyResource> GetPoliciesByApiAsync(string apiManagementResourceId, string apiName);

        Task<ApiOperationPolicyResource> GetPoliciesByOperationAsync(string apiManagementResourceId, string apiName, string operationId);

        Task<ApiManagementPolicyResource> GetGlobalApimPolicyAsync(string apiManagementResourceId);
    }
}
