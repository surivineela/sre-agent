// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Framework;
using Agent.Core.Attributes;
using Agent.Plugins.Interface;
using Agent.Core.Models;

namespace Agent.Plugins.Definitions
{
    /// <summary>
    /// Role Assignment Plugin Definition
    /// </summary>
    [AgentToolPlugin(Category = ToolCategories.RoleAssignment)]
    public class RoleAssignmentPluginDefinition
    {
        private readonly IRoleAssignmentPlugin _roleAssignmentPlugin;

        /// <summary>
        /// Initializes a new instance of the <see cref="RoleAssignmentPluginDefinition"/> class.
        /// </summary>
        /// <param name="roleAssignmentPlugin">Implementation of the role assignment plugin</param>
        public RoleAssignmentPluginDefinition(IRoleAssignmentPlugin roleAssignmentPlugin)
        {
            _roleAssignmentPlugin = roleAssignmentPlugin;
        }

        [Description("Gets all role assignments for a specific user/managed identity on an Azure resource. "
            + "If principalId is null or empty, all role assignments on the resource are returned.")]
        public async Task<string> GetRoleAssignments(
            [Description("The full ARM resource ID (e.g., /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.Storage/storageAccounts/{name})")]
            string resourceId,
            [Description("(Optional) The principal ID (Object ID) of the user or managed identity. Leave empty to get all role assignments on the resource.")]
            string principalId = "")
        {
            return await _roleAssignmentPlugin.GetRoleAssignmentsAsync(resourceId, principalId);
        }

        [Description("Adds a role assignment for a user or managed identity on an Azure resource")]
        [RequiresApproval]
        [WriteAction]
        public async Task<string> AddRoleAssignment(
            [Description("The full ARM resource ID (e.g., /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.Storage/storageAccounts/{name})")]
            string resourceId,
            [Description("The type of principal being added, must be either 'User' or 'ServicePrincipal'")]
            string principalType,
            [Description("The principal ID (Object ID) of the user or managed identity")]
            string principalId,
            [Description("The name of the role to assign (e.g., 'Storage Blob Data Owner')")]
            string roleName)
        {
            return await _roleAssignmentPlugin.AddRoleAssignmentAsync(resourceId, principalType, principalId, roleName);
        }

        [Description("Removes a role assignment for a user or managed identity on an Azure resource")]
        [RequiresApproval]
        [WriteAction]
        public async Task<string> RemoveRoleAssignment(
            [Description("The full ARM resource ID (e.g., /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.Storage/storageAccounts/{name})")]
            string resourceId,
            [Description("The principal ID (Object ID) of the user or managed identity")]
            string principalId,
            [Description("The name of the role to remove (e.g., 'Storage Blob Data Owner')")]
            string roleName)
        {
            return await _roleAssignmentPlugin.RemoveRoleAssignmentAsync(resourceId, principalId, roleName);
        }

        [Description("Checks if a user or managed identity has a specific role on an Azure resource")]
        public async Task<string> CheckRoleAssignment(
            [Description("The full ARM resource ID (e.g., /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.Storage/storageAccounts/{name})")]
            string resourceId,
            [Description("The principal ID (Object ID) of the user or managed identity")]
            string principalId,
            [Description("The name of the role to check (e.g., 'Storage Blob Data Owner')")]
            string roleName)
        {
            return await _roleAssignmentPlugin.CheckRoleAssignmentAsync(resourceId, principalId, roleName);
        }

        [Description("Gets details of the role definition for a specified role name that can be applied to the resource.")]
        public async Task<string> GetRoleDetailsFromNameAsync(
            [Description("The name of the role to check (e.g., 'Storage Blob Data Owner')")]
            string roleName,
            [Description("The full ARM resource ID (e.g., /subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.Storage/storageAccounts/{name})")]
            string resourceId)
        {
            return await _roleAssignmentPlugin.GetRoleDetailsFromNameAsync(roleName, resourceId);
        }
    }
}
