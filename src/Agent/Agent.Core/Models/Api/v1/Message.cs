namespace Agent.Core.Models.Api.v1;

public record Message(
    Guid Id,
    DateTime TimeStamp,
    Author Author,
    string Text,
    bool IsImageContent = false,
    Posted? Posted = null
);

public record Posted(
    bool Teams
);

public record Attachment(
    string Url,
    string Name,
    string Typep
);