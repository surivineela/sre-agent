// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Plugins.Definitions;
using Azure;
using Azure.Core;
using Azure.ResourceManager.Authorization;
using Azure.ResourceManager.Authorization.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

/// <summary>
/// Plugin to manage role assignments for users or managed identities against ARM resources.
/// </summary>
public class RoleAssignmentPlugin : IRoleAssignmentPlugin
{
    private readonly ILogger<RoleAssignmentPlugin> _logger;
    private readonly IArmClientFactory _armClientFactory;
    private readonly ArmHelper _armHelper;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleAssignmentPlugin"/> class.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="armClientFactory">ARM client factory</param>
    /// <param name="armHelper">ARM helper</param>
    public RoleAssignmentPlugin(
        ILogger<RoleAssignmentPlugin> logger,
        IArmClientFactory armClientFactory,
        ArmHelper armHelper)
    {
        _logger = logger;
        _armClientFactory = armClientFactory;
        _armHelper = armHelper;
    }

    /// <summary>
    /// Gets role assignments for a specified principal (user or managed identity) on a resource.
    /// </summary>
    /// <param name="resourceId">The full ARM resource ID</param>
    /// <param name="principalId">The principal ID (Object ID) of the user or managed identity</param>
    /// <returns>JSON string containing details of role assignments</returns>
    public async Task<string> GetRoleAssignmentsAsync(string resourceId, string principalId)
    {

        try
        {
            if(string.IsNullOrWhiteSpace(resourceId))
            {
                return "ERROR: Resource ID cannot be null or empty.";
            }

            if (!_armHelper.IsWellFormattedResourceId(resourceId))
            {
                return "ERROR: Invalid resource ID format.";
            }

            if(!string.IsNullOrEmpty(principalId) && !Guid.TryParseExact(principalId, "D", out _))
            {
                return "ERROR: Invalid principal ID format. Principal id must be a valid GUID, separated by hyphens";
            }

            var armClient = _armClientFactory.GetArmClient();
            var resource = armClient.GetGenericResource(new ResourceIdentifier(resourceId));
            var roleAssignments = new List<object>();

            // Get role assignments at the resource level
            await foreach (var assignment in resource.GetRoleAssignments().GetAllAsync())
            {
                if (string.IsNullOrWhiteSpace(principalId) || assignment.Data.PrincipalId?.ToString().Equals(principalId, StringComparison.OrdinalIgnoreCase) == true)
                {
                    roleAssignments.Add(new
                    {
                        RoleAssignmentId = assignment.Data.Name,
                        RoleDefinitionId = assignment.Data.RoleDefinitionId.ToString(),
                        RoleName = await GetRoleNameFromDefinitionIdAsync(assignment.Data.RoleDefinitionId.ToString()),
                        PrincipalId = assignment.Data.PrincipalId.ToString(),
                        PrincipalType = assignment.Data.PrincipalType?.ToString() ?? "Unknown",
                        Scope = assignment.Data.Scope
                    });
                }
            }

            return JsonSerializer.Serialize(roleAssignments, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving role assignments for resource {resourceId} and principal {principalId}");
            return $"ERROR: Failed to retrieve role assignments: {ex.Message}";
        }
    }

    /// <summary>
    /// Adds a role assignment for a principal on a specific resource.
    /// </summary>
    /// <param name="resourceId">The full ARM resource ID</param>
    /// <param name="principalType">The type of principal ID to assign role to. Can be either User or ServicePrincipal</param>
    /// <param name="principalId">The principal ID (Object ID) of the user or managed identity</param>
    /// <param name="roleName">The name of the role to assign (e.g., "Storage Blob Data Owner")</param>
    /// <returns>Result of the role assignment operation</returns>
    public async Task<string> AddRoleAssignmentAsync(string resourceId, string principalType, string principalId, string roleName)
    {
        try
        {
            if (!_armHelper.IsWellFormattedResourceId(resourceId))
            {
                return "ERROR: Invalid resource ID format.";
            }

            if (string.IsNullOrEmpty(principalId) || !Guid.TryParseExact(principalId, "D", out _))
            {
                return "ERROR: Invalid principal ID. Principal id must be a valid GUID, separated by hyphens";
            }

            if (!"User".Equals(principalType, StringComparison.OrdinalIgnoreCase) && !"ServicePrincipal".Equals(principalType, StringComparison.OrdinalIgnoreCase))
            {
                return "ERROR: Invalid principal type. Principal type must be either 'User' or 'ServicePrincipal'.";
            }

            var armClient = _armClientFactory.GetArmClient();
            var roleDefinitionId = await GetRoleDefinitionIdFromNameAsync(roleName, resourceId);

            if (string.IsNullOrEmpty(roleDefinitionId))
            {
                return $"ERROR: Role '{roleName}' not found.";
            }

            var resourceIdentifier = new ResourceIdentifier(resourceId);

            var resource = armClient.GetGenericResource(new ResourceIdentifier(resourceId));
            var roleAssignmentsCollection = resource.GetRoleAssignments();

            // Check if assignment already exists
            bool alreadyExists = false;
            await foreach (var assignment in roleAssignmentsCollection.GetAllAsync())
            {
                if (assignment.Data.Scope.Equals(resourceId, StringComparison.OrdinalIgnoreCase) &&
                    assignment.Data.PrincipalId?.ToString().Equals(principalId, StringComparison.OrdinalIgnoreCase) == true &&
                    assignment.Data.RoleDefinitionId?.ToString().Equals(roleDefinitionId, StringComparison.OrdinalIgnoreCase) == true)
                {
                    alreadyExists = true;
                    break;
                }
            }

            if (alreadyExists)
            {
                return $"Role assignment for role '{roleName}' already exists for the specified principal on this resource.";
            }

            // Create a new role assignment
            var roleAssignmentId = Guid.NewGuid().ToString();
            var roleAssignmentCollection = resource.GetRoleAssignments();

            var roleAssignmentData = new RoleAssignmentCreateOrUpdateContent(
                roleDefinitionId: new ResourceIdentifier(roleDefinitionId),
                principalId: new Guid(principalId))
            {
                PrincipalType = principalType?.Equals("User", StringComparison.OrdinalIgnoreCase) == true ? RoleManagementPrincipalType.User: RoleManagementPrincipalType.ServicePrincipal // Default to service principal
            };
            
            // Create the role assignment at the resource scope
            var roleAssignmentOperation = await roleAssignmentCollection.CreateOrUpdateAsync(
                WaitUntil.Completed,
                roleAssignmentId,
                roleAssignmentData);

            return $"Successfully assigned role '{roleName}' to principal {principalId} on resource {resourceId}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding role assignment for resource {ResourceId}, principal {PrincipalId}, and role {RoleName}", resourceId, principalId, roleName);
            return $"ERROR: Failed to add role assignment: {ex.Message}";
        }
    }

    /// <summary>
    /// Removes a role assignment for a principal on a specific resource.
    /// </summary>
    /// <param name="resourceId">The full ARM resource ID</param>
    /// <param name="principalId">The principal ID (Object ID) of the user or managed identity</param>
    /// <param name="roleName">The name of the role to remove (e.g., "Storage Blob Data Owner")</param>
    /// <returns>Result of the role removal operation</returns>
    public async Task<string> RemoveRoleAssignmentAsync(string resourceId, string principalId, string roleName)
    {
        try
        {
            if (!_armHelper.IsWellFormattedResourceId(resourceId))
            {
                return "ERROR: Invalid resource ID format.";
            }

            var armClient = _armClientFactory.GetArmClient();
            var roleDefinitionId = await GetRoleDefinitionIdFromNameAsync(roleName, resourceId);

            if (string.IsNullOrEmpty(roleDefinitionId))
            {
                return $"ERROR: Role '{roleName}' not found.";
            }

            if (string.IsNullOrEmpty(principalId) || !Guid.TryParseExact(principalId, "D", out _))
            {
                return "ERROR: Invalid principal ID. Principal id must be a valid GUID, separated by hyphens";
            }

            var resourceIdentifier = new ResourceIdentifier(resourceId);
            var resource = armClient.GetGenericResource(new ResourceIdentifier(resourceId));
            var roleAssignmentsCollection = resource.GetRoleAssignments();

            RoleAssignmentResource roleAssignmentToDelete = null;

            await foreach (var assignment in roleAssignmentsCollection.GetAllAsync())
            {
                if (assignment.Data.Scope.Equals(resourceId, StringComparison.OrdinalIgnoreCase) &&
                    assignment.Data.PrincipalId?.ToString().Equals(principalId, StringComparison.OrdinalIgnoreCase) == true &&
                    assignment.Data.RoleDefinitionId?.ToString().Equals(roleDefinitionId, StringComparison.OrdinalIgnoreCase) == true)
                {
                    roleAssignmentToDelete = assignment;
                    break;
                }
            }

            if (roleAssignmentToDelete != null)
            {
                await roleAssignmentToDelete.DeleteAsync(WaitUntil.Completed);
                return $"Successfully removed role '{roleName}' from principal {principalId} on resource {resourceId}.";
            }
            else
            {
                return $"No matching role assignment found for role '{roleName}' and principal {principalId} on this resource.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing role assignment for resource {ResourceId}, principal {PrincipalId}, and role {RoleName}", resourceId, principalId, roleName);
            return $"ERROR: Failed to remove role assignment: {ex.Message}";
        }
    }

    /// <summary>
    /// Checks if a principal has a specific role on a resource.
    /// </summary>
    /// <param name="resourceId">The full ARM resource ID</param>
    /// <param name="principalId">The principal ID (Object ID) of the user or managed identity</param>
    /// <param name="roleName">The name of the role to check (e.g., "Storage Blob Data Owner")</param>
    /// <returns>True if the principal has the specified role, otherwise false</returns>
    public async Task<string> CheckRoleAssignmentAsync(string resourceId, string principalId, string roleName)
    {
        try
        {
            if (!_armHelper.IsWellFormattedResourceId(resourceId))
            {
                return "ERROR: Invalid resource ID format.";
            }

            if (string.IsNullOrWhiteSpace(roleName))
            {
                return "ERROR: Role name cannot be empty.";
            }

            if (string.IsNullOrEmpty(principalId) || !Guid.TryParseExact(principalId, "D", out _))
            {
                return "ERROR: Invalid principal ID. Principal id must be a valid GUID, separated by hyphens";
            }
            
            var armClient = _armClientFactory.GetArmClient();
            var roleDefinitionId = await GetRoleDefinitionIdFromNameAsync(roleName, resourceId);

            if (string.IsNullOrEmpty(roleDefinitionId))
            {
                return $"ERROR: Role '{roleName}' not found.";
            }

            var resourceIdentifier = new ResourceIdentifier(resourceId);
            var resource = armClient.GetGenericResource(new ResourceIdentifier(resourceId));
            var roleAssignmentsCollection = resource.GetRoleAssignments();

            bool hasRole = false;
            RoleAssignmentData existingRoleAssignment = null;
            await foreach (var assignment in roleAssignmentsCollection.GetAllAsync())
            {
                if (assignment.Data.Scope.Equals(resourceId, StringComparison.OrdinalIgnoreCase) &&
                    assignment.Data.PrincipalId?.ToString().Equals(principalId, StringComparison.OrdinalIgnoreCase) == true &&
                    assignment.Data.RoleDefinitionId?.ToString().Equals(roleDefinitionId, StringComparison.OrdinalIgnoreCase) == true)
                {
                    hasRole = true;
                    existingRoleAssignment = assignment.Data;
                    break;
                }
            }

            return JsonSerializer.Serialize(new { HasRole = hasRole, RoleName = roleName, RoleDescription = existingRoleAssignment?.Description ?? string.Empty,  PrincipalId = principalId },
                new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking role assignment for resource {resourceId}, principal {principalId}, and role {roleName}");
            return $"ERROR: Failed to check role assignment: {ex.Message}";
        }
    }

    /// <summary>
    /// Gets details of the role definition for a specified role name that can be applied to the resource.
    /// </summary>
    /// <param name="roleName">Role name to get details for</param>
    /// <param name="resourceId">The full ARM resource ID</param>
    /// <returns>Details of the specified role</returns>
    public async Task<string> GetRoleDetailsFromNameAsync(string roleName, string resourceId)
    {
        if(string.IsNullOrWhiteSpace(roleName))
        {
            return "ERROR: Role name cannot be null or empty.";
        }

        if (!_armHelper.IsWellFormattedResourceId(resourceId))
        {
            return "ERROR: Invalid resource ID format.";
        }

        try
        {
            var armClient = _armClientFactory.GetArmClient();
            var resourceIdentifier = new ResourceIdentifier(resourceId);
            //var subscription = armClient.GetSubscriptionResource(resourceIdentifier.Parent.Parent);
            //var roleDefinitionsCollection = subscription.GetRoleDefinitions();
            var resource = armClient.GetGenericResource(new ResourceIdentifier(resourceId));
            var roleDefinitionsCollection = resource.GetAuthorizationRoleDefinitions();
            await foreach (var role in roleDefinitionsCollection.GetAllAsync())
            {
                if (role.Data.RoleName.Equals(roleName, StringComparison.OrdinalIgnoreCase))
                {
                    return JsonSerializer.Serialize(new
                    {
                        RoleName = role.Data.RoleName,
                        RoleType = role.Data.RoleType,
                        RoleId = role.Data.Id,
                        Description = role.Data.Description,
                        AssignableScopes = role.Data.AssignableScopes,
                        RolePermissions = role.Data.Permissions
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
            }
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role details for role name {RoleName}", roleName);
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the role definition ID from a role name that can be applied to the resource.
    /// </summary>
    private async Task<string?> GetRoleDefinitionIdFromNameAsync(string roleName, string resourceId)
    {
        try
        {
            if (!_armHelper.IsWellFormattedResourceId(resourceId))
            {
                return "ERROR: Invalid resource ID format.";
            }
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return "ERROR: Role name cannot be empty.";
            }
            var armClient = _armClientFactory.GetArmClient();
            var resourceIdentifier = new ResourceIdentifier(resourceId);

            //var subscription = armClient.GetSubscriptionResource(resourceIdentifier.Parent.Parent);
            //var roleDefinitionsCollection = subscription.GetRoleDefinitions();

            var resource = armClient.GetGenericResource(new ResourceIdentifier(resourceId));
            var roleDefinitionsCollection = resource.GetAuthorizationRoleDefinitions();

            await foreach (var role in roleDefinitionsCollection.GetAllAsync())
            {
                if (role.Data.RoleName.Equals(roleName, StringComparison.OrdinalIgnoreCase))
                {
                    return role.Id.ToString();
                }
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role definition ID for role name {RoleName}", roleName);
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the role name from a role definition ID.
    /// </summary>
    private async Task<string> GetRoleNameFromDefinitionIdAsync(string roleDefinitionId)
    {
        try
        {
            var armClient = _armClientFactory.GetArmClient();
            var roleDefResourceId = new ResourceIdentifier(roleDefinitionId);
            var subscription = armClient.GetSubscriptionResource(roleDefResourceId.Parent.Parent);
            var roleDefinition = await subscription.GetAuthorizationRoleDefinitionAsync(roleDefResourceId);

            if (string.IsNullOrWhiteSpace(roleDefinition.Value.Data.RoleName))
            {
                _logger.LogWarning("Role name is empty for role definition ID {RoleDefinitionId}", roleDefinitionId);
            }
            return roleDefinition.Value.Data.RoleName ?? "Unknown Role";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role name from definition ID {RoleDefinitionId}", roleDefinitionId);
            return "Unknown Role";
        }
    }
}
