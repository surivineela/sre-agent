namespace Agent.Core.Models;

public sealed record SqlServerLocalAuthStatus(
    string ResourceId,
    string Name,
    string Location,
    bool IsAzureADOnlyAuthenticationEnabled,
    bool IsEntraAdminSet
    );
