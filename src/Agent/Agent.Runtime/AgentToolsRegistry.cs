// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Linq.Expressions;
using Agent.Runtime.Helpers;

namespace Agent.Runtime;

public class AgentToolsRegistry
{
    private readonly List<string> _toolSignatures = new();

    public AgentToolsRegistry()
    {
    }

    public IReadOnlyList<string> ToolSignatures => _toolSignatures.AsReadOnly();

    /// <summary>
    /// Registers a specific method from a plugin class as a tool
    /// </summary>
    /// <typeparam name="T">The plugin definition type</typeparam>
    /// <param name="executeFunctionSelector">Expression that selects the method to register</param>
    public void RegisterTool<T>(Expression<Func<T, Delegate>> executeFunctionSelector)
    {
        _toolSignatures.AddTool(executeFunctionSelector);
    }


    /// <summary>
    /// Registers all methods with Description attribute from a plugin class
    /// </summary>
    /// <typeparam name="T">The plugin definition type</typeparam>
    public void RegisterPlugin<T>()
    {
        _toolSignatures.AddPlugin<T>();
    }
}
