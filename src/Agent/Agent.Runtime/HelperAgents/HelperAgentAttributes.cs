// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Runtime.HelperAgents;

/// <summary>
/// This attribute is used to mark a method as the entry point of a helper agent.
/// This method should return quickly and is required.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class HelperAgentEntryPointAttribute : Attribute
{
}

/// <summary>
/// This attribute is used to mark a method as the long-running operation of a helper agent
/// that will eventually return a result. This method is optional, and if present it must have
/// the same parameters as the entry point method.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class HelperAgentLongRunningAttribute : Attribute
{
}

/// <summary>
/// This attribute is used to mark a method as a plugin for a helper agent. It must have
/// the same signature as the entry point method on the target agent type.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class HelperAgentPluginAttribute : Attribute
{
    public required Type AgentInputType { get; set; }
}

