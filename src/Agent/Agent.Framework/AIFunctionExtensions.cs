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
        return function.GetType().GetCustomAttribute<AgentToolAttribute>()?.Mode ?? defaultMode;
    }
}
