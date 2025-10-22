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


    /// <summary>
    /// Condition to control enabling/disabling the plugin.
    /// Supports two formats:
    /// 1. Environment variable check: "EnvVarName:ExpectedValue" (e.g., "FeatureFlagX:Enabled")
    /// 2. Data connector type check: "DataConnectorType:ConnectorType" (e.g., "DataConnectorType:Teams")
    ///    Checks if a data connector of the specified type exists in the DataConnectors configuration.
    /// </summary>
    public string EnabledIf { get; set; } = string.Empty;

    public AgentToolPluginAttribute() { }
}
