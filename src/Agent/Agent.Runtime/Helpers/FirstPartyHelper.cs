// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Runtime.Helpers;

/// <summary>
/// Helper class for determining first-party tenant and agent type information.
/// </summary>
public static class FirstPartyHelper
{
    /// <summary>
    /// List of known first-party tenant IDs.
    /// </summary>
    private static readonly List<string> FirstPartyTenants = new()
    {
        "33e01921-4d64-4f8c-a055-5bdaffd5e33d",
        "72f988bf-86f1-41af-91ab-2d7cd011db47",
        "975f013f-7f24-47e8-a7d3-abc4752bf346",
        "cdc5aeea-15c5-4db6-b079-fcadd2505dc2"
    };

    /// <summary>
    /// Determines if the current execution context is running as a first-party tenant.
    /// This checks both ACA agent type and tenant ID against the known first-party tenants list.
    /// </summary>
    /// <returns>True if this is a first-party tenant, false otherwise.</returns>
    public static bool IsFirstPartyTenant()
    {
        var isAcaAgent = Environment.GetEnvironmentVariable("AGENT_TYPE_NAME") == "ACAAgent";
        var tenantId = Environment.GetEnvironmentVariable("AppSettings__Core__Azure__Crawler__TenantId") ?? string.Empty;
        var isTenantFirstParty = !string.IsNullOrWhiteSpace(tenantId) && FirstPartyTenants.Contains(tenantId);

        return isAcaAgent || isTenantFirstParty;
    }

    /// <summary>
    /// Checks if the current agent is running in ACA (Azure Container Apps) mode.
    /// </summary>
    /// <returns>True if this is an ACA agent, false otherwise.</returns>
    public static bool IsAcaAgent()
    {
        return Environment.GetEnvironmentVariable("AGENT_TYPE_NAME") == "ACAAgent";
    }

    /// <summary>
    /// Gets the current tenant ID from configuration.
    /// </summary>
    /// <returns>The tenant ID if configured, empty string otherwise.</returns>
    public static string GetTenantId()
    {
        return Environment.GetEnvironmentVariable("AppSettings__Core__Azure__Crawler__TenantId") ?? string.Empty;
    }

    /// <summary>
    /// Checks if the specified tenant ID is in the first-party tenants list.
    /// </summary>
    /// <param name="tenantId">The tenant ID to check.</param>
    /// <returns>True if the tenant ID is a first-party tenant, false otherwise.</returns>
    public static bool IsTenantFirstParty(string tenantId)
    {
        return !string.IsNullOrWhiteSpace(tenantId) && FirstPartyTenants.Contains(tenantId);
    }
}
