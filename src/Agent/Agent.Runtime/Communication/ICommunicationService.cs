namespace Agent.Runtime.Communication;

public interface ICommunicationService
{
    Task SendMessageAsync(string threadId, string message);
    Task NotifyCompletionAsync(string threadId, string instanceId, string status, string? summary = null);
}