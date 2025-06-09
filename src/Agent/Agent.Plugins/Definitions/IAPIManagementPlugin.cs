using Agent.Plugins.Models;

namespace Agent.Plugins.Definitions
{
    public interface IAPIManagementPlugin
    {
        Task<IReadOnlyList<APIManagementDescriptor>> ListAPIManagementAsync(Guid subscriptionId);

        Task<APIManagementDescriptor> GetAPIManagementInfoAsync(string resourceId);

        Task<string> GetAPIMErrorLogsAsync(string apimInstanceResourceId, DateTime startTime, DateTime endTime, string statusCode, int top = 6);
    }
}
