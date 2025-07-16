// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class AgentToolAttribute : Attribute
{
    public AgentToolAttribute(ToolMode mode)
    {
        Mode = mode;
        Category = string.Empty;
        ResourceType = string.Empty;
    }

    public AgentToolAttribute(ToolMode mode, string category, string resourceType)
    {
        Mode = mode;
        Category = category;
        ResourceType = resourceType;
    }

    public ToolMode Mode { get; }
    public string Category { get; set; }
    public string ResourceType { get; set; }
}

public enum ToolMode
{
    Auto,
    Manual
}
