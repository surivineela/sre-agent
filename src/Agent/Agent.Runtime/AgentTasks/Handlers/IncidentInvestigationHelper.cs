// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text.Json;
using Agent.Core.Attributes;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Plugins.Definitions;

namespace Agent.Runtime.AgentTasks.Handlers;

public static class IncidentInvestigationHelper
{
    public static bool FilterTools(MethodInfo methodInfo)
    {
        if (methodInfo.GetCustomAttribute<WriteActionAttribute>() is not null)
        {
            return false;
        }

        if (methodInfo.GetCustomAttribute<RequiresApprovalAttribute>() is not null)
        {
            return false;
        }

        if (methodInfo.DeclaringType == typeof(UserInteractionPluginDefinition) ||
            methodInfo.DeclaringType == typeof(AgentControlFlowPluginDefinition) ||
            methodInfo.DeclaringType == typeof(AgentReasoningControlFlowPluginDefinition) ||
            methodInfo.DeclaringType == typeof(AgentInteractionPluginDefinition))
        {
            return false;
        }

        return true;
    }
}
