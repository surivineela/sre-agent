using Agent.Plugins.Models;

namespace Agent.Plugins.Interface
{
    public interface IAPIManagementPlugin
    {
        Task<IReadOnlyList<APIManagementDescriptor>> ListAPIManagementAsync(Guid subscriptionId);

        Task<APIManagementDescriptor> GetAPIManagementInfoAsync(string resourceId);

        Task<string> GetAPIMErrorLogsAsync(string apimInstanceResourceId, DateTime startTime, DateTime endTime, string statusCode, int top = 6);

        Task<string> GetAPIMActivityLogs(string apimResourceId, DateTime startTime, DateTime endTime);
    }
}
