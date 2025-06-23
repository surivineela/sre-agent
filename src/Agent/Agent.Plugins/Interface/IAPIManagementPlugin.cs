using Agent.Plugins.Models;

namespace Agent.Plugins.Interface
{
    public interface IAPIManagementPlugin
    {
        Task<List<APIManagementDescriptor>> ListAPIManagementAsync(Guid subscriptionId);

        Task<APIManagementDescriptor?> GetAPIManagementInfoAsync(string resourceId);

        Task<string> GetAPIMErrorLogsAsync(string apimInstanceResourceId, DateTime startTime, DateTime endTime, string statusCode, int top);

        Task<string> GetAPIMFailureRateByApiOperation(string apiManagementResourceId, DateTime startTime, DateTime endTime);

        Task<string> GetAPIMRecentFailedRequests(string apiManagementResourceId, TimeSpan lookback, int topN);

        Task<string> GetAPIMActivityLogs(string apimResourceId, DateTime startTime, DateTime endTime);

        Task<List<APIManagementApiDescriptor>> GetAPIMApis(string apiManagementResourceId, string workspaceName);

        Task<APIManagementApiDescriptor> GetAPIDetailsByName(string apiManagementResourceId, string apiName, string workspaceName);

        Task<List<APIManagementApiOperationSummary>> GetAPIOperationsByApi(string apiManagementResourceId, string apiName, string workspaceName);

        Task<APIManagementApiOperationDescriptor> GetAPIOperationDetailedInfo(string apiManagementResourceId, string apiName, string operationName, string workspaceName);
    }
}
