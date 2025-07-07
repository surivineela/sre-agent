using Agent.Core.Models.Api.v1;

namespace Agent.Plugins.Interface;

public interface IAzureDevOpsWorkItemPlugin
{
    Task<string> CreateWorkItem(string resourceId, string title, string description, string[] tags = null, string assignedTo = null, string areaPath = "", string iterationPath = "", string workItemType = "Task", string priority = "Medium", string severity = "None", string state = "New");
    Task<string> GetIaCForAzureDevOps(string resourceId, string branch, string fileMatches);
    Task<string> FindConnectedRepository(string resourceId);
    Task<AzureDevOpsAccessToken> GetToken();
}
