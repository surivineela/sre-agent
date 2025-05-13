namespace Agent.Core.Models;

public sealed record ServiceBusLocalAuthStatus(
    string ResourceId,
    string Name,
    string Location,
    bool IsLocalAuthDisabled
    );
