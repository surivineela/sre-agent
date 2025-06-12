// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Interface;

/// <summary>
/// Interface defining operations for managing role assignments for users or managed identities against ARM resources
/// </summary>
public interface IRoleAssignmentPlugin
{
    /// <summary>
    /// Gets role assignments for a specified principal (user or managed identity) on a resource.
    /// </summary>
    /// <param name="resourceId">The full ARM resource ID</param>
    /// <param name="principalId">The principal ID (Object ID) of the user or managed identity</param>
    /// <returns>JSON string containing details of role assignments</returns>
    Task<string> GetRoleAssignmentsAsync(string resourceId, string principalId);

    /// <summary>
    /// Adds a role assignment for a principal on a specific resource.
    /// </summary>
    /// <param name="resourceId">The full ARM resource ID</param>
    /// <param name="principalType">The type of principal ID to assign role to. Can be either User or ServicePrincipal</param>
    /// <param name="principalId">The principal ID (Object ID) of the user or managed identity</param>
    /// <param name="roleName">The name of the role to assign (e.g., "Storage Blob Data Owner")</param>
    /// <returns>Result of the role assignment operation</returns>
    Task<string> AddRoleAssignmentAsync(string resourceId, string principalType, string principalId, string roleName);

    /// <summary>
    /// Removes a role assignment for a principal on a specific resource.
    /// </summary>
    /// <param name="resourceId">The full ARM resource ID</param>
    /// <param name="principalId">The principal ID (Object ID) of the user or managed identity</param>
    /// <param name="roleName">The name of the role to remove (e.g., "Storage Blob Data Owner")</param>
    /// <returns>Result of the role removal operation</returns>
    Task<string> RemoveRoleAssignmentAsync(string resourceId, string principalId, string roleName);

    /// <summary>
    /// Checks if a principal has a specific role on a resource.
    /// </summary>
    /// <param name="resourceId">The full ARM resource ID</param>
    /// <param name="principalId">The principal ID (Object ID) of the user or managed identity</param>
    /// <param name="roleName">The name of the role to check (e.g., "Storage Blob Data Owner")</param>
    /// <returns>True if the principal has the specified role, otherwise false</returns>
    Task<string> CheckRoleAssignmentAsync(string resourceId, string principalId, string roleName);

    /// <summary>
    /// Gets details of the role definition for a specified role name that can be applied to the resource.
    /// </summary>
    /// <param name="roleName">Role name to get details for</param>
    /// <param name="resourceId">The full ARM resource ID</param>
    /// <returns>Details of the specified role</returns>
    Task<string> GetRoleDetailsFromNameAsync(string roleName, string resourceId);
}
