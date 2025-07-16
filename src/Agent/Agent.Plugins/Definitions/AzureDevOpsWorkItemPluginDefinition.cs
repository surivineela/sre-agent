using System.ComponentModel;
using Agent.Core.Models;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin(Category = ToolCategories.DevOps)]
public class AzureDevOpsWorkItemPluginDefinition
{
    private readonly IAzureDevOpsWorkItemPlugin _azureDevOpsWorkItemPlugin;

    public AzureDevOpsWorkItemPluginDefinition(IAzureDevOpsWorkItemPlugin azureDevOpsWorkItemPlugin)
    {
        _azureDevOpsWorkItemPlugin = azureDevOpsWorkItemPlugin;
    }

    [Description("Create a work item in Azure DevOps (AzDo/TFS). Creates any work item type: tasks, user stories, bugs, features, epics, test cases, issues, tickets, cards. Works with linked repositories. Use for any request to add, create, make, generate, file, track, or manage work items regardless of phrasing ('add task', 'create bug', 'make story', 'file ticket', 'track work', 'add to backlog', 'create issue', 'new item', etc.). Handles all work tracking scenarios in Azure DevOps.")]
    public async Task<string> CreateAzureDevOpsWorkItem([Description("The resource ID of the Azure Resource for example: /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourceGroups/mrsharm-operations-agent-3p-rg/providers/Microsoft.App/containerApps/memory-leak-app/containerapp")] string resourceId,
                                                        [Description("Title of the WorkItem")] string title,
                                                        [Description("Description to be filled in the body of the work item as well formatted markdown.")] string description,
                                                        [Description("An array of tags to be used in the work item based on the description.")] string[] tags)
    {
        return await _azureDevOpsWorkItemPlugin.CreateWorkItem(resourceId, title, description, tags: tags);
    }

    [Description("Finds the connected or linked Azure DevOps (AzDo/TFS) repository for a given Azure Resource ID. Locates associated repos, git repositories, source code repositories, or code bases linked to Azure resources. Works with any Azure resource type (App Service, Function App, Container Instance, AKS, etc.). Use for requests to find, locate, discover, identify, or get the repository, repo, source code, git repo, or code base connected to Azure resources. Handles variations like 'what repo is linked to this resource', 'find the source code', 'get the repository', 'where is the code', etc.")]
    public async Task<string> FindConnectedRepository([Description("The resource ID of the Azure Resource for example: /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourceGroups/mrsharm-operations-agent-3p-rg/providers/Microsoft.App/containerApps/memory-leak-app/containerapp")] string resourceId)
    {
        return await _azureDevOpsWorkItemPlugin.FindConnectedRepository(resourceId);
    }

    [Description("Connects or links an Azure Resource to an Azure DevOps (AzDo) repository. For example: 'Connect the albumapicsharp-2 app with the https://dev.azure.com/iactest7758/TestApp/_git/TestApp repository' or 'Link the memory-leak-app app with the https://dev.azure.com/iactest7758/TestApp/_git/TestApp repository'.")]
    public async Task<string> ConnectRepositoryToResource([Description("The resource ID of the Azure Resource for example:  /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourceGroups/mrsharm-operations-agent-3p-rg/providers/Microsoft.App/containerApps/memory-leak-app/containerapp")] string resourceId,
                                                          [Description("The Azure DevOps repository url.")] string repositoryUrl)
    {
        return await _azureDevOpsWorkItemPlugin.ConnectRepository(resourceId, repositoryUrl);
    }

    [Description("Disconnects or unlinks an Azure Resource from an Azure DevOps (AzDo) repository. For example: 'Disconnect the albumapicsharp-2 app from the connected repository' or 'Unlink the memory-leak-app app from the from the connected repository'.")]
    public async Task<string> DisconnectRepositoryFromResource([Description("The resource ID of the Azure Resource for example: /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourceGroups/mrsharm-operations-agent-3p-rg/providers/Microsoft.App/containerApps/memory-leak-app/containerapp")] string resourceId)
    {
        return await _azureDevOpsWorkItemPlugin.DisconnectRepository(resourceId);
    }

    [Description("Retrieves Infrastructure as Code (IaC) files and identifies the IaC type used in the connected Azure DevOps (AzDo) repository for a given Azure Resource. Analyzes and returns IaC templates, configuration files, and deployment scripts from the linked repository. Supports Bicep, ARM templates, Terraform, YAML, and JSON configurations. Use for requests to find, get, analyze, identify, or discover infrastructure code, deployment templates, IaC files, configuration files, or infrastructure definitions associated with Azure resources. Handles variations like 'what IaC is used', 'get the infrastructure code', 'find deployment templates', 'analyze the infrastructure files', etc.")]
    public async Task<string> GetIaCForAzureDevOps([Description("The resource ID of the Azure Resource for example: /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourceGroups/mrsharm-operations-agent-3p-rg/providers/Microsoft.App/containerApps/memory-leak-app/containerapp")] string resourceId,
                                                   [Description("The branch - if no value is provided, assume 'main'")] string branch,
                                                   [Description("Comma separated file patterns to match for retrieving files (e.g. '*.bicep,*.json')")] string fileMatches = "*bicep,*yaml,*yml,*json,*tf*")

    {
        return await _azureDevOpsWorkItemPlugin.GetIaCForAzureDevOps(resourceId, branch, fileMatches);
    }
}
