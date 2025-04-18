namespace Agent.Core.Models.Api.v1;

public record ReasoningMessage(
    Guid Id,
    Guid AgentContextId,
    ReasoningMessageRoleEnum Role,
    string SerializedChatMessage);
