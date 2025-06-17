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
    }

    public ToolMode Mode { get; }
}

public enum ToolMode
{
    Auto,
    Manual
}
