// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Web.Models.ExtendedAgents.Request;

/// <summary>
/// Request model for executing a system tool
/// </summary>
public class SystemToolExecutionRequest
{
    /// <summary>
    /// Name of the system tool to execute
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the plugin that provides the tool
    /// </summary>
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    /// Parameters to pass to the tool as key-value pairs
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}
