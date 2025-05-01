namespace Agent.Core.Models;

public sealed record SqlServerSettings(
    string ResourceId,
    string Name,
    string Location,
    bool IsAzureADOnlyAuthenticationEnabled,
    bool IsEntraAdminSet
    );
