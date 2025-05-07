namespace Agent.Core.Models;

public sealed record CosmosDbStatus(
    string ResourceId,
    string Name,
    string Location,
    bool IsLocalAuthEnabled
    );
