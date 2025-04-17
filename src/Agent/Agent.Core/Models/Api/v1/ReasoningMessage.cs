namespace Agent.Core.Models.Api.v1;

public record ReasoningMessage(
    Guid Id,
    Guid SubAgentThreadId,
    ReasoningMessageRoleEnum Role,
    string? Text,
    FunctionInvocation? FunctionInvocation);
