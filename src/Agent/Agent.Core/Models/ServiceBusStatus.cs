namespace Agent.Core.Models;

public sealed record ServiceBusStatus(
    string ResourceId,
    string Name,
    string Location,
    bool IsLocalAuthDisabled
    );
