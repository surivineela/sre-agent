namespace Agent.Core.Models;

public sealed record EventHubLocalAuthStatus(
    string ResourceId,
    string Name,
    string Location,
    bool IsLocalAuthDisabled
    );
