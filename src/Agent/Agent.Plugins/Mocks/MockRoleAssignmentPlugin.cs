using System.Text.Json;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Mocks;
public class MockRoleAssignmentPlugin : IRoleAssignmentPlugin
{
    public Task<string> AddRoleAssignmentAsync(string resourceId, string principalType, string principalId, string roleName)
    {
        return Task.FromResult($"Role '{roleName}' assigned to {principalType} with ID '{principalId}' on resource '{resourceId}'.");
    }

    public Task<string> CheckRoleAssignmentAsync(string resourceId, string principalId, string roleName)
    {
        return Task.FromResult($"Role '{roleName}' is assigned to principal with ID '{principalId}' on resource '{resourceId}'.");
    }

    public Task<string> GetRoleAssignmentsAsync(string resourceId, string principalId)
    {
        return Task.FromResult($"Role assignments for resource '{resourceId}' and principal ID '{principalId}': [Role1, Role2]");
    }

    public Task<string> GetRoleDetailsFromNameAsync(string roleName, string resourceId)
    {
        return Task.FromResult(JsonSerializer.Serialize(new
        {
            RoleName = roleName,
            RoleType = "BuiltInRole",
            RoleId = "/subscriptions/subID/providers/Microsoft.Authorization/roleDefinitions/roleDefinitionId",
            Description = "Mock rule",
            AssignableScopes = new List<string>() { resourceId},
            RolePermissions = new List<string>() { "Read", "Write" }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    public Task<string> RemoveRoleAssignmentAsync(string resourceId, string principalId, string roleName)
    {
        return Task.FromResult($"Role '{roleName}' removed from principal with ID '{principalId}' on resource '{resourceId}'.");
    }
}
