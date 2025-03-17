namespace Agent.Core.Models.Api.v1;

public record ThreadMessage(
    Guid ThreadId,
    string Message,
    string UserId,
    string DisplayName,
    DateTime Timestamp);

public record InboundServiceResponse(
    Guid ThreadId,
    Guid MessageId,
    string OrchestrationInstanceId);
