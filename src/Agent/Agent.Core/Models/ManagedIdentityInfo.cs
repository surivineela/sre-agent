namespace Agent.Core.Models;

public class ManagedIdentityInfo
{
    public bool IsConnected { get; set; }
    public string RepoUrl { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string WorkflowPath { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
