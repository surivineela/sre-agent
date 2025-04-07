
namespace Agent.Core.Models;
public sealed record SupportProductFromArmModel(
    string id,
    string name,
    string type,
    SupportProductFromArmPropertiesModel properties
    );

public sealed record SupportProductFromArmPropertiesModel(
    string displayName,
    List<string> resourceTypes,
    SupportProductFromArmPropertiesMetadataModel metadata
    );

public sealed record SupportProductFromArmPropertiesMetadataModel(
    string state,
    string groupIds,
    string legacyId,
    string serviceIdentifierName
    );
