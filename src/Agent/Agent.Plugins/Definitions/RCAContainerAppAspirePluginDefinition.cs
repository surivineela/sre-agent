// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
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

    [Description(@"""
    Purpose:
    Determines if a container app environment supports workload profiles (V2 environment type).

    Scenario:
    Use this tool to verify environment compatibility for Aspire functionality. Aspire is only available on V2 environments with workload profiles.
    This is typically the first diagnostic step to determine if Aspire can be enabled or accessed.

    Output:
    Returns tab-separated table data in CSV format. Column headers:
    - hasWorkloadProfiles: Boolean value indicating if workload profiles exist (true = V2 environment, false = V1 environment)
    """
    )]
    public Task<string> CheckContainerAppWorkloadProfileExists(
        [Description("Azure region.")] string region,
        [Description("Managed cluster name.")] string managedClusterName,
        [Description("Start time of the query.")] DateTime fromDate,
        [Description("End time of the query.")] DateTime toDate)
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

    [Description(@"""
    Purpose:
    Retrieves the environment name associated with a managed cluster for environment identification.

    Scenario:
    Use this tool to resolve the environment name from a managed cluster name. This is essential for subsequent
    Aspire-specific queries that require the exact environment identifier.

    Output:
    Returns tab-separated table data in CSV format. Column headers:
    - environmentName: The environment name associated with the specified managed cluster
    """
    )]
    public Task<string> GetContainerAppEnvironmentName(
        [Description("Azure region.")] string region,
        [Description("Managed cluster name.")] string managedClusterName,
        [Description("Start time of the query.")] DateTime fromDate,
        [Description("End time of the query.")] DateTime toDate)
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

    [Description(@"""
    Purpose:
    Verifies if Aspire is enabled and active for a specific container app environment.

    Scenario:
    Use this tool to confirm Aspire configuration status by checking for authentication events. This determines
    if Aspire dashboard is properly configured and has been accessed by users.

    Output:
    Returns tab-separated table data in CSV format. Column headers:
    - IsAspireEnabled: Boolean value indicating if Aspire is enabled (true = enabled and active, false = not enabled or inactive)
    """
    )]
    public Task<string> CheckIfAspireIsEnabled(
        [Description("Azure region.")] string region,
        [Description("Managed cluster name.")] string managedClusterName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("CheckIfAspireIsEnabled", region,
            new Dictionary<string, string>
            {
                {"managedClusterName", managedClusterName },
                {"region", region },
            });
    }

    [Description(@"""
    Purpose:
    Analyzes Envoy controller logs for 404 errors specifically related to Aspire endpoint routing.

    Scenario:
    Use this tool when investigating Aspire dashboard access issues. 404 errors indicate that Envoy controller
    is missing Aspire routes and needs to be restarted for route reconciliation.

    Output:
    Returns tab-separated table data in CSV format. Column headers:
    - EnvironmentName: Name of the container app environment
    - Count404Errors: Number of 404 errors for Aspire endpoints
    """
    )]
    public Task<string> CheckEnvoyFrontEndLogs(
        [Description("Azure region.")] string region,
        [Description("Managed cluster name.")] string managedClusterName,
        [Description("Start time of the query.")] DateTime fromDate,
        [Description("End time of the query.")] DateTime toDate)
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

    [Description(@"""
    Purpose:
    Identifies authentication failures when users attempt to access the Aspire dashboard.

    Scenario:
    Use this tool to detect authentication issues during the initial login flow. This helps identify problems
    with auth code detection, redirects to Microsoft login, or SSO endpoint communication failures.

    Output:
    Returns tab-separated table data in CSV format. Column headers:
    - AuthFailureCount: Number of authentication failure events detected in the specified time range
    """
    )]
    public Task<string> CheckAspireDashboardAccess(
        [Description("Azure region.")] string region,
        [Description("Managed cluster name.")] string managedClusterName,
        [Description("Start time of the query.")] DateTime fromDate,
        [Description("End time of the query.")] DateTime toDate)
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

    [Description(@"""
    Purpose:
    Detects authorization failures (403 errors) when users fail permission validation for Aspire dashboard access.

    Scenario:
    Use this tool when investigating permission-related access issues. Authorization failures indicate users
    lack sufficient RBAC permissions (Contributor or Owner) on the managed environment.

    Output:
    Returns tab-separated table data in CSV format. Column headers:
    - AuthFailureCount: Number of authorization failure events with 403 status codes
    """
    )]
    public Task<string> CheckAspireAuthorizationIssues(
        [Description("Azure region.")] string region,
        [Description("Managed cluster name.")] string managedClusterName,
        [Description("Start time of the query.")] DateTime fromDate,
        [Description("End time of the query.")] DateTime toDate)
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

    [Description(@"""
    Purpose:
    Analyzes VNET configuration for container app environments to identify network-related authentication issues.

    Scenario:
    Use this tool when investigating authentication failures that may be related to VNET integration.
    Custom VNETs can block access to SSO endpoints required for Aspire authentication.

    Output:
    Returns tab-separated table data in CSV format. Column headers:
    - customVnet: Boolean indicating if environment uses custom VNET
    - vnetSubscriptionId: Subscription ID of the VNET (if custom VNET)
    - vnetResourcegroup: Resource group name of the VNET (if custom VNET)
    - vnetName: Name of the VNET (if custom VNET)
    - subnetName: Name of the subnet (if custom VNET)
    """
    )]
    public Task<string> CheckEnvironmentVnet(
        [Description("Azure region.")] string region,
        [Description("Managed cluster name.")] string managedClusterName,
        [Description("Start time of the query.")] DateTime fromDate,
        [Description("End time of the query.")] DateTime toDate)
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

    [Description(@"""
    Purpose:
    Identifies state verification failures during Aspire authentication flow caused by SSO endpoint accessibility issues.

    Scenario:
    Use this tool when investigating authentication failures related to state verification. These failures typically
    occur when the authentication flow cannot access the SSO state endpoint due to network restrictions.

    Output:
    Returns tab-separated table data in CSV format. Column headers:
    - StateVerificationFailureCount: Number of state verification failure events where SSO state retrieval failed
    """
    )]
    public Task<string> CheckAspireStateVerificationIssues(
        [Description("Azure region.")] string region,
        [Description("Managed cluster name.")] string managedClusterName,
        [Description("Start time of the query.")] DateTime fromDate,
        [Description("End time of the query.")] DateTime toDate)
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
