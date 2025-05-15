// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;

namespace Agent.Runtime.HelperAgents;

public static class HelperAgentExtensions
{
    public static MethodInfo? GetEntryPointMethod(this HelperAgent helperAgent)
    {
        return helperAgent.GetType().GetMethods().FirstOrDefault(m => m.GetCustomAttribute<HelperAgentEntryPointAttribute>() != null);
    }

    public static MethodInfo? GetLongRunningMethod(this HelperAgent helperAgent)
    {
        return helperAgent.GetType().GetMethods().FirstOrDefault(m => m.GetCustomAttribute<HelperAgentLongRunningAttribute>() != null);
    }

    public static bool IsAsync(this HelperAgent helperAgent)
    {
        return helperAgent.GetLongRunningMethod() != null;
    }
}
