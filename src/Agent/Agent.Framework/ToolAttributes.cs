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

    /// <summary>
    /// When set to true, the output of this tool will not be truncated by ToolOutputProcessService,
    /// regardless of size. Use this for tools where the full output is critical for the agent's operation.
    /// </summary>
    public bool DisableOutputTruncation { get; set; }

    /// <summary>
    /// When set to true, the tool's return value will be returned as-is without JSON serialization.
    /// This is useful for tools that return complex objects that need to be processed by the caller directly.
    /// </summary>
    public bool KeepOriginalReturnType { get; set; }
}

public enum ToolMode
{
    Auto,
    Manual
}
