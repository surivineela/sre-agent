
namespace Agent.Core.Models;
public sealed record GenericArmResourceModel(
    string id,
    string name,
    string type,
    string kind,
    string location,
    object properties,
    IReadOnlyDictionary<string, string> tags);
