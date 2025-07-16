using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    public AgentToolPluginAttribute() { }
}
