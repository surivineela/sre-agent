namespace Agent.Core.Models;

public sealed record StorageAccountStatus(
    string ResourceId,
    string Name,
    string Location,
    bool StorageKeyEnabled,
    bool PublicContainersEnabled
    );
