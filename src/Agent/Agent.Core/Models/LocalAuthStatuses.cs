namespace Agent.Core.Models;

public sealed record AppServiceLocalAuthStatus(
    string ResourceId,
    string Name,
    string Location,
    bool SCMBasicAuthEnabled,
    bool FTPBasicAuthEnabled
    );

public sealed record CosmosDbLocalAuthStatus(
    string ResourceId,
    string Name,
    string Location,
    bool IsLocalAuthEnabled
    );

public sealed record EventHubLocalAuthStatus(
    string ResourceId,
    string Name,
    string Location,
    bool IsLocalAuthDisabled
    );

public sealed record ServiceBusLocalAuthStatus(
    string ResourceId,
    string Name,
    string Location,
    bool IsLocalAuthDisabled
    );

public sealed record SqlServerLocalAuthStatus(
    string ResourceId,
    string Name,
    string Location,
    bool IsAzureADOnlyAuthenticationEnabled,
    bool IsEntraAdminSet
    );

public sealed record StorageAccountLocalAuthSettings(
    string ResourceId,
    string Name,
    string Location,
    bool StorageKeyEnabled,
    bool PublicContainersEnabled
    );

public sealed record KubernetesLocalAuthStatus(
    string ResourceId,
    string Name,
    string Location,
    bool DisableLocalAccounts
    );
