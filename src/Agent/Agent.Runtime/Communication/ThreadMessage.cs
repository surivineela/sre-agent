namespace Agent.Runtime.Communication;

public record ThreadMessage(
    string ThreadId,
    string Message,
    string UserId,
    DateTime Timestamp);
