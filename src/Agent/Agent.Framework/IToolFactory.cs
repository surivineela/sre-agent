// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

public enum BehaviorOnNameConflict
{
    /// <summary>
    /// If a function with the same name already exists, throw an exception.
    /// </summary>
    ThrowException,

    /// <summary>
    /// If a function with the same name already exists, ignore the new function.
    /// </summary>
    Ignore,

    /// <summary>
    /// If a function with the same name already exists, overwrite the existing function.
    /// </summary>
    Overwrite
}

public interface IToolFactory<TContext> : IAsyncInitializer
    where TContext : class
{
    /// <summary>
    /// Find an AI function by its name.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException">Thrown when the function with the specified name is not found.</exception>
    public AIFunction GetTool(string name);

    public AIFunction GetTool(string name, Guid threadId);

    /// <summary>
    /// Find an AI function by its name with agent mode support.
    /// </summary>
    /// <param name="name">The name of the tool</param>
    /// <param name="threadId">The thread ID</param>
    /// <param name="agentMode">The agent mode (e.g., "Chat", "ReadOnly", "Review", "Autonomous")</param>
    /// <returns></returns>
    public AIFunction GetTool(string name, Guid threadId, string? agentMode);

    public bool TryFindTool(string name, out AIFunction? function);

    public bool HasTool(string name);

    public Task FindAndRegisterAllToolsAsync(BehaviorOnNameConflict onNameConflict);

    public List<ToolInfo> FetchAvailableToolInfo(Func<MethodInfo, bool>? filter = null);

    public void RegisterExtendedToolFromModel(string extendedToolName, string extendedToolYaml);
    bool RegisterTool(YamlToolDefinitionBase tool, BehaviorOnNameConflict onNameConflict);
}

public class ToolInfo
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IList<string?> Parameters { get; set; } = [];
    // Add new properties for incident handler tools
    public bool IsIncidentHandlerTool { get; set; } = false;
    public string? IncidentHandlerPlatform { get; set; }
}

