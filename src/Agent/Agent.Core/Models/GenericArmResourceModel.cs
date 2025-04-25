
namespace Agent.Core.Models;
public sealed record GenericArmResourceModel(
    string id,
    string name,
    string type,
    string kind,
    string location,
    object properties,
    IReadOnlyDictionary<string, string> tags,
    List<GenericIdentityModel> IdentityModels);

public sealed record GenericIdentityModel(IdentityType identityType, Guid principalId);

public enum IdentityType
{
    UserAssignedManagedIdentity,
    SystemAssignedManagedIdentity
}
