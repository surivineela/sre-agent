namespace Agent.Runtime.Communication;

public class ThreadOrchestrationMapping
{
    public string ThreadId { get; set; } = string.Empty;
    public string OrchestrationInstanceId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}