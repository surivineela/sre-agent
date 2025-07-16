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

    public static string GetToolCategory(this AIFunction function, string defaultCategory = "")
    {
        var category = function.UnderlyingMethod?.GetCustomAttribute<AgentToolAttribute>()?.Category;
        return string.IsNullOrWhiteSpace(category) ? defaultCategory : category;
    }

    public static string GetToolResourceType(this AIFunction function, string defaultResourceType = "")
    {
        var resourceType = function.UnderlyingMethod?.GetCustomAttribute<AgentToolAttribute>()?.ResourceType;
        return string.IsNullOrWhiteSpace(resourceType) ? defaultResourceType : resourceType;
    }

    public static bool IsAgentAsTool(this AIFunction tool)
    {
        return tool.GetType().IsGenericType && tool.GetType().GetGenericTypeDefinition() == typeof(AgentAsTool<>);
    }
}
