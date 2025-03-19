namespace Agent.Core.Models.Api.v1;

public record Message(
    Guid Id,
    DateTime TimeStamp,
    Author Author,
    string Text,
    Posted? Posted = null
);

public record Posted(
    bool Teams
);