namespace Agent.Core.Models.Api.v1;

public record WaitInformation(
    DateTime? WaitUntil,
    string? Reason);
