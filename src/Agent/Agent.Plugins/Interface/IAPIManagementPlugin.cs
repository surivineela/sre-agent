using Agent.Plugins.Models;

namespace Agent.Plugins.Interface
{
    public interface IAPIManagementPlugin
    {
        Task<IReadOnlyList<APIManagementDescriptor>> ListAPIManagementAsync(Guid subscriptionId);

        Task<APIManagementDescriptor> GetAPIManagementInfoAsync(string resourceId);

        Task<string> GetAPIMErrorLogsAsync(string apimInstanceResourceId, DateTime startTime, DateTime endTime, string statusCode, int top);

        Task<string> GetAPIMFailureRateByApiOperation(string apiManagementResourceId, DateTime startTime, DateTime endTime);

        Task<string> GetAPIMRecentFailedRequests(string apiManagementResourceId, TimeSpan lookback, int topN);

        Task<string> GetAPIMActivityLogs(string apimResourceId, DateTime startTime, DateTime endTime);
    }
}
