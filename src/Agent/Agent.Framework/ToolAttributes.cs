// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

// Usage of this attribute is to mark classes that hold tools for agents to use.
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class AgentToolPluginAttribute : Attribute
{
}

[AttributeUsageAttribute(AttributeTargets.Method, AllowMultiple = false)]
public class AgentToolAttribute : Attribute
{
    public AgentToolAttribute(ToolMode mode)
    {
        Mode = mode;
    }

    public ToolMode Mode { get; }
}

public enum ToolMode
{
    Auto,
    Manual
}
