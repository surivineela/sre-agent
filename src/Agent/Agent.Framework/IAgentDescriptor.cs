namespace Agent.Framework;

public interface IAgentDescriptor
{
    public string Name { get; set; }
    public string Instructions { get; set; }
    public string? HandoffDescription { get; set; }
    public List<string> Handoffs { get; set; }
    public List<string> AutoTools { get; set; }
    public List<string> ManualTools { get; set; }
    public int MaxReflectionCount { get; set; }
    public string CustomReflectionNote { get; set; }
}
