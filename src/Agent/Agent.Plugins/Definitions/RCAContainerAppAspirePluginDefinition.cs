using System.ComponentModel;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin(Category = ToolCategories.Configuration, ResourceType = ToolResourceTypes.ContainerApps)]
public class RCAContainerAppAspirePluginDefinition
{
    private readonly IKustoPlugin _kustoPlugin;

    public RCAContainerAppAspirePluginDefinition(IKustoPlugin kustoPlugin)
    {
        _kustoPlugin = kustoPlugin;
    }

    [Description(@"
    Purpose:
    Checks if the container app environment has a workload profile.

    Scenario:
    Use this tool to determine if a workload profile exists in a container app environment.

    Output:
    Returns true if the workload profile exists, otherwise false.
    ")]

    public Task<string> CheckContainerAppWorkloadProfileExists(string region, string managedClusterName, DateTime fromDate, DateTime toDate)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("CheckContainerAppWorkloadProfileExists", region,
            new Dictionary<string, string>
            {
                { "managedClusterName", managedClusterName },
                { "region", region },
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() }
            });
    }

    [Description(@"
    Purpose:
    Retrieves the environment name associated with a managed cluster.

    Scenario:
    Use this tool to get the environment name for a specific managed cluster.

    Output:
    Returns the environment name associated with the specified managed cluster. If multiple environments are found, returns all environment names. If no matching environment is found, returns a message indicating no environment was found.
    ")]
    public Task<string> GetContainerAppEnvironmentName(string region, string managedClusterName, DateTime fromDate, DateTime toDate)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetContainerAppEnvironmentName", region,
            new Dictionary<string, string>
            {
                  { "managedClusterName", managedClusterName },
                  { "region", region },
                  { "fromDate", fromDate.ToString() },
                  { "toDate", toDate.ToString() }
            });
    }

    [Description(@"
    Purpose:
    Checks if Aspire is enabled for a specific container app environment.

    Scenario:
    Use this tool to determine if Aspire is active in a container app environment.

    Output:
    Returns the count of active DotNet component operations in the environment. A count greater than 0 indicates that Aspire is enabled and active in the environment. If no DotNet component is found or if it's deleted, returns 0.
    ")]
    public Task<string> CheckIfAspireIsEnabled(string region, string environmentName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("CheckIfAspireIsEnabled", region,
            new Dictionary<string, string>
            {
                {"environmentName", environmentName },
                {"region", region },
            });
    }

    [Description(@"
    Purpose:
    Checks for 404 errors in Envoy controller logs related to Aspire endpoints.

    Scenario:
    Use this tool to identify issues with Aspire routing in the Envoy controller.

    Output:
    Returns a count of 404 errors grouped by managedClusterName, name, targetNamespace, and endpoint. This helps identify issues with Aspire routing in the Envoy controller.
    ")]
    public Task<string> CheckEnvoyFrontEndLogs(string region, string managedClusterName, DateTime fromDate, DateTime toDate)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("CheckEnvoyFrontEndLogs", region,
            new Dictionary<string, string>
            {
                {"managedClusterName", managedClusterName },
                {"region", region },
                {"fromDate", fromDate.ToString() },
                {"toDate", toDate.ToString() }
            });
    }

    [Description(@"
    Purpose:
    Checks for successful access to the Aspire dashboard.

    Scenario:
    Use this tool to identify potential authentication issues with the Aspire dashboard.

    Output:
    Returns counts of successful (200/302) dashboard access requests and any other status codes. This helps identify potential authentication issues with the Aspire dashboard.
    ")]
    public Task<string> CheckAspireDashboardAccess(string region, string managedClusterName, DateTime fromDate, DateTime toDate)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("CheckAspireDashboardAccess", region,
            new Dictionary<string, string>
            {
                {"managedClusterName", managedClusterName },
                {"region", region },
                {"fromDate", fromDate.ToString() },
                {"toDate", toDate.ToString() }
            });
    }


    [Description(@"
    Purpose:
    Checks for authorization issues when accessing the Aspire dashboard.

    Scenario:
    Use this tool to identify permission-related issues with the Aspire dashboard access.

    Output:
    Returns a count of authorization failures where users failed during authentication with 403 errors. This helps identify permission-related issues with the Aspire dashboard access.
    ")]
    public Task<string> CheckAspireAuthorizationIssues(string region, string managedClusterName, DateTime fromDate, DateTime toDate)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("CheckAspireAuthorizationIssues", region,
            new Dictionary<string, string>
            {
                {"managedClusterName", managedClusterName },
                {"region", region },
                {"fromDate", fromDate.ToString() },
                {"toDate", toDate.ToString() }
            });
    }

    [Description(@"
    Purpose:
    Checks if the container app environment has VNET configured.

    Scenario:
    Use this tool to verify VNET configuration for a container app environment.

    Output:
    Returns VNET configuration details including subscription ID, resource group, VNET name and the subnet name.
    ")]
    public Task<string> CheckEnvironmentVnet(string region, string managedClusterName, DateTime fromDate, DateTime toDate)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("CheckEnvironmentVnet", region,
            new Dictionary<string, string>
            {
                {"managedClusterName", managedClusterName },
                {"region", region },
                {"fromDate", fromDate.ToString() },
                {"toDate", toDate.ToString() }
            });
    }

    [Description(@"
    Purpose:
    Checks for state verification issues in external authentication for Aspire dashboard.

    Scenario:
    Use this tool to identify networking issues with accessing authentication endpoints.

    Output:
    Returns a count of state verification failures when accessing the SSO endpoint. This helps identify networking issues with accessing authentication endpoints.
    ")]
    public Task<string> CheckAspireStateVerificationIssues(string region, string managedClusterName, DateTime fromDate, DateTime toDate)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("CheckAspireStateVerificationIssues", region,
            new Dictionary<string, string>
            {
            {"managedClusterName", managedClusterName },
            {"region", region },
            {"fromDate", fromDate.ToString() },
            {"toDate", toDate.ToString() }
            });
    }
}
