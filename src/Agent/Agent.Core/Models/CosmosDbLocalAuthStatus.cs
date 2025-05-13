namespace Agent.Core.Models;

public sealed record CosmosDbLocalAuthStatus(
    string ResourceId,
    string Name,
    string Location,
    bool IsLocalAuthEnabled
    );
