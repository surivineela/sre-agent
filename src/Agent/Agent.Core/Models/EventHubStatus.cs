namespace Agent.Core.Models;

public sealed record EventHubStatus(
    string ResourceId,
    string Name,
    string Location,
    bool IsLocalAuthDisabled
    );
