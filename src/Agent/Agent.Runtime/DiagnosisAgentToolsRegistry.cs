// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Linq.Expressions;
using Agent.Runtime.Helpers;

namespace Agent.Runtime;

public class DiagnosisAgentToolsRegistry
{
    private readonly List<string> _toolSignatures = new();

    public DiagnosisAgentToolsRegistry()
    {
    }

    public IReadOnlyList<string> ToolSignatures => _toolSignatures.AsReadOnly();

    /// <summary>
    /// Registers a specific method from a plugin class as a tool
    /// </summary>
    /// <typeparam name="T">The plugin definition type</typeparam>
    /// <param name="executeFunctionSelector">Expression that selects the method to register</param>
    public void RegisterReadOnlyTool<T>(Expression<Func<T, Delegate>> executeFunctionSelector)
    {
        _toolSignatures.AddToolNoApprovalRequired(executeFunctionSelector);
    }

    /// <summary>
    /// Registers all methods with a Description attribute and no RequiresApproval attribute from a plugin class
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public void RegisterReadOnlyPlugin<T>()
    {
        _toolSignatures.AddPluginNoApprovalRequired<T>();
    }
}
