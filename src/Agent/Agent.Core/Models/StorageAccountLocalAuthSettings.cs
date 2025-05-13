namespace Agent.Core.Models;

public sealed record StorageAccountLocalAuthSettings(
    string ResourceId,
    string Name,
    string Location,
    bool StorageKeyEnabled,
    bool PublicContainersEnabled
    );
