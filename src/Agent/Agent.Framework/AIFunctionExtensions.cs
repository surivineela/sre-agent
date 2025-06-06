// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

public static class AIFunctionExtensions
{
    public static ToolMode GetToolMode(this AIFunction function, ToolMode defaultMode = ToolMode.Manual)
    {
        return function.UnderlyingMethod?.GetCustomAttribute<AgentToolAttribute>()?.Mode ?? defaultMode;
    }

    public static bool IsAgentAsTool(this AIFunction tool)
    {
        return tool.GetType().IsGenericType && tool.GetType().GetGenericTypeDefinition() == typeof(AgentAsTool<>);
    }
}
