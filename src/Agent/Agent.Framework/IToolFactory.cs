// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

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

public interface IToolFactory<TContext> where TContext : class
{
    /// <summary>
    /// Find an AI function by its name.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException">Thrown when the function with the specified name is not found.</exception>
    public AIFunction GetTool(string name);

    public bool TryFindTool(string name, out AIFunction? function);

    public bool HasTool(string name);

    public List<ToolInfo> FetchAvailableToolInfo();
}

public class ToolInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string[] Parameters { get; set; } = [];
}

