// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace Agent.Framework;

public static class AIFunctionExtensions
{
    public static ToolMode GetToolMode(this AIFunction function, ToolMode defaultMode = ToolMode.Manual)
    {
        if (function.AdditionalProperties is not null &&
            TryParseToolMode(function.AdditionalProperties, out var modeFromAdditional))
        {
            return modeFromAdditional;
        }

        return function.UnderlyingMethod?.GetToolMode(defaultMode) ?? defaultMode;
    }

    public static ToolMode GetToolMode(this MethodInfo methodInfo, ToolMode defaultMode = ToolMode.Manual)
    {
        return methodInfo.GetCustomAttribute<AgentToolAttribute>()?.Mode ?? defaultMode;
    }

    private static bool TryParseToolMode(IReadOnlyDictionary<string, object?> properties, out ToolMode mode)
    {
        if (properties.TryGetValue("tool_mode", out var value))
        {
            switch (value)
            {
                case ToolMode toolMode:
                    mode = toolMode;
                    return true;
                case string toolModeString when Enum.TryParse(toolModeString, true, out ToolMode parsed):
                    mode = parsed;
                    return true;
            }
        }

        mode = default;
        return false;
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

    public static bool IsHandoff(this AIFunction tool)
    {
        return tool.GetType().IsGenericType && tool.GetType().GetGenericTypeDefinition() == typeof(Handoff<>);
    }

    public static bool IsMcpTool(this AIFunction tool)
    {
        // Native MCP client tool OR runtime wrapper named McpToolAIFunction
        return tool is McpClientTool || tool.GetType().Name == "McpToolAIFunction";
    }
}
