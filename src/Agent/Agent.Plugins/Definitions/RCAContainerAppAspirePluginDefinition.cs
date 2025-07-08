using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin]
public class RCAContainerAppAspirePluginDefinition
{
    private readonly IKustoPluginChat _kustoPlugin;

    public RCAContainerAppAspirePluginDefinition(IKustoPluginChat kustoPlugin)
    {
        _kustoPlugin = kustoPlugin;
    }

    [Description(@"
This operation will check if the container app environment has a workload profile.

Input parameters:
- region: The Azure region where the container app is hosted
- managedClusterName: The name of the managed cluster
- fromDate: The start date for the query
- toDate: The end date for the query

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
This operation retrieves the environment name associated with a managed cluster.

Input parameters:
- region: The Azure region where the container app is hosted (lowercase with no spaces, e.g., 'westeurope')
- managedClusterName: The name of the managed cluster
- fromDate: The start date for the query
- toDate: The end date for the query

Output:
Returns the environment name associated with the specified managed cluster. If multiple environments are found,
returns all environment names. If no matching environment is found, returns a message indicating no environment was found.
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
This operation will check if Aspire is enabled for a specific container app environment.

Input parameters:
- region: The Azure region where the container app is hosted (lowercase with no spaces, e.g., 'westeurope'
- environmentName: The name of the container app environment

Output:
Returns the count of active DotNet component operations in the environment . A count greater than 0 indicates
that Aspire is enabled and active in the environment. If no DotNet component is found or if it's deleted, returns 0.
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
This operation checks for 404 errors in Envoy controller logs related to Aspire endpoints.

Input parameters:
- region: The Azure region where the container app is hosted (lowercase with no spaces, e.g., 'westeurope'
- managedClusterName: The name of the managed cluster
- fromDate: The start date for the query
- toDate: The end date for the query

Output:
Returns a count of 404 errors grouped by managedClusterName, name, targetNamespace, and endpoint.
This helps identify issues with Aspire routing in the Envoy controller.
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
This operation checks for successful access to the Aspire dashboard.

Input parameters:
- region: The Azure region where the container app is hosted (lowercase with no spaces, e.g., 'westeurope'
- managedClusterName: The name of the managed cluster
- fromDate: The start date for the query
- toDate: The end date for the query

Output:
Returns counts of successful (200/302) dashboard access requests and any other status codes.
This helps identify potential authentication issues with the Aspire dashboard.
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
This operation checks for authorization issues when accessing the Aspire dashboard.

Input parameters:
- region: The Azure region where the container app is hosted (lowercase with no spaces, e.g., 'westeurope'
- managedClusterName: The name of the managed cluster
- fromDate: The start date for the query
- toDate: The end date for the query

Output:
Returns a count of authorization failures where users failed during authentication with 403 errors.
This helps identify permission-related issues with the Aspire dashboard access.
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
    This operations checks if the container app environment has VNET configured.
    Input parameters:
    - region: The Azure region where the container app is hosted (lowercase with no spaces, e.g., 'westeurope')
    - managedClusterName: The name of the managed cluster
    - fromDate: The start date for the query
    - toDate: The end date for the query
    Output:
    Returns VNET configuration details and details like subscription ID, resource group, VNET name and the subnet name
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
This operation checks for state verification issues in external authentication for Aspire dashboard.

Input parameters:
- region: The Azure region where the container app is hosted (lowercase with no spaces, e.g., 'westeurope'
- managedClusterName: The name of the managed cluster
- fromDate: The start date for the query
- toDate: The end date for the query

Output:
Returns a count of state verification failures when accessing the SSO endpoint.
This helps identify networking issues with accessing authentication endpoints.
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
