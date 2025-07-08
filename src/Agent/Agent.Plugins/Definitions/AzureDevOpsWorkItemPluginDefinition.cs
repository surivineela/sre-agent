using System.ComponentModel;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin]
public class AzureDevOpsWorkItemPluginDefinition
{
    private readonly IAzureDevOpsWorkItemPlugin _azureDevOpsWorkItemPlugin;

    public AzureDevOpsWorkItemPluginDefinition(IAzureDevOpsWorkItemPlugin azureDevOpsWorkItemPlugin)
    {
        _azureDevOpsWorkItemPlugin = azureDevOpsWorkItemPlugin;
    }

    [Description("Create a work item in Azure DevOps (AzDo)")]
    public async Task<string> CreateAzureDevOpsWorkItem([Description("The resource ID of the Azure Resource for example: /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourceGroups/mrsharm-operations-agent-3p-rg/providers/Microsoft.App/containerApps/memory-leak-app/containerapp")] string resourceId,
                                                        [Description("Title of the WorkItem")] string title,
                                                        [Description("Description to be filled in the body of the work item as well formatted markdown.")] string description,
                                                        [Description("An array of tags to be used in the work item based on the description.")] string[] tags)
    {
        return await _azureDevOpsWorkItemPlugin.CreateWorkItem(resourceId, title, description, tags: tags);
    }

    [Description("Finds the connected Azure DevOps (AzDo) repository for a given Azure Resource ID.")]
    public async Task<string> FindConnectedRepository([Description("The resource ID of the Azure Resource for example: /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourceGroups/mrsharm-operations-agent-3p-rg/providers/Microsoft.App/containerApps/memory-leak-app/containerapp")] string resourceId)
    {
        return await _azureDevOpsWorkItemPlugin.FindConnectedRepository(resourceId);
    }

    [Description("Connects an Azure Resource to an Azure DevOps (AzDo) repository. For example: Connect the albumapicsharp-2 app with the https://dev.azure.com/iactest7758/TestApp/_git/TestApp repository")]
    public async Task<string> ConnectRepositoryToResource([Description("The resource ID of the Azure Resource for example:  /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourceGroups/mrsharm-operations-agent-3p-rg/providers/Microsoft.App/containerApps/memory-leak-app/containerapp")] string resourceId,
                                                          [Description("The Azure DevOps repository url.")] string repositoryUrl)
    {
        return await _azureDevOpsWorkItemPlugin.ConnectRepository(resourceId, repositoryUrl);
    }

    [Description("Gets the type of Infrastructure as Code (IaC) - this is the most likely type of IaC used.")]
    public async Task<string> GetIaCForAzureDevOps([Description("The resource ID of the Azure Resource for example: /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourceGroups/mrsharm-operations-agent-3p-rg/providers/Microsoft.App/containerApps/memory-leak-app/containerapp")] string resourceId,
                                                   [Description("The branch - if no value is provided, assume 'main'")] string branch,
                                                   [Description("Comma separated file patterns to match for retrieving files (e.g. '*.bicep,*.json')")] string fileMatches = "*bicep,*yaml,*yml,*json,*tf*")

    {
        return await _azureDevOpsWorkItemPlugin.GetIaCForAzureDevOps(resourceId, branch, fileMatches);
    }
}
