using Agent.Core.Configuration;

namespace Agent.Plugins;

// Usage of this attribute is to mark classes that hold tools for agents to use.
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class AgentToolPluginAttribute : Attribute
{
    public bool IsEnabled { get; set; } = true;
    public bool IsFirstPartyOnly { get; set; } = false;
    public bool IsExperimental { get; set; } = false;
    public string Category { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    
    // Add new properties for incident handler tools
    public bool IsIncidentHandlerPlugin { get; set; } = false;
    public IncidentManagementType IncidentPlatform { get; set; } = IncidentManagementType.Icm;

    public AgentToolPluginAttribute() { }
}
