namespace Agent.Runtime.Communication;

public record AgentMessage(
    string ThreadId,
    string Message,
    string MessageType,
    DateTime Timestamp);
