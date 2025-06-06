using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Agent.Framework;
using Microsoft.Extensions.AI;

namespace Agent.Tests.Common.Mocks.FunctionCalling;

/// <summary>
/// A replay-enabled implementation of IToolFactory that uses stored replay data
/// to return pre-recorded function results instead of executing actual functions.
/// Falls back to the inner factory when no replay data is available.
/// </summary>
/// <typeparam name="TContext">The context type for the tool factory</typeparam>
public class ReplayToolFactory<TContext> : IToolFactory<TContext> where TContext : class
{
    private readonly IToolFactory<TContext> _innerFactory;
    private readonly ReplayToolCore _replayCore;

    public IEnumerable<string> FunctionNames => _replayCore.FunctionNames;
    public HashSet<string> FunctionNamesEnabledForReplay => _replayCore.FunctionNamesEnabledForReplay;
    public List<ReplayEntry> FunctionCallsWithReplayFailure => _replayCore.FunctionCallsWithReplayFailure;

    public ReplayToolFactory(IToolFactory<TContext> innerFactory, JsonSerializerOptions serializerOptions)
    {
        _innerFactory = innerFactory ?? throw new ArgumentNullException(nameof(innerFactory));
        _replayCore = new ReplayToolCore(serializerOptions ?? throw new ArgumentNullException(nameof(serializerOptions)));
    }

    /// <summary>
    /// Loads replay data from a log string.
    /// </summary>
    public void LoadLogFromString(string logContent)
    {
        _replayCore.LoadLogFromString(logContent);
    }

    /// <summary>
    /// Gets a tool (AIFunction) by name, returning a replayed version if available,
    /// otherwise falls back to the inner factory.
    /// </summary>
    public AIFunction GetTool(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Function name cannot be null or empty.", nameof(name));
        }

        // Get the original function from the inner factory
        var originalFunction = _innerFactory.GetTool(name);

        // Use CreateReplayWrapper to wrap it with replay functionality if appropriate
        return _replayCore.CreateReplayWrapper(name, originalFunction);
    }

    public AIFunction GetTool(string name, Guid threadId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Function name cannot be null or empty.", nameof(name));
        }

        // Get the original function from the inner factory
        var originalFunction = _innerFactory.GetTool(name, threadId);

        // Use CreateReplayWrapper to wrap it with replay functionality if appropriate
        return _replayCore.CreateReplayWrapper(name, originalFunction);
    }

    /// <summary>
    /// Tries to find a tool by name, returning a replayed version if available,
    /// otherwise falls back to the inner factory.
    /// </summary>
    public bool TryFindTool(string name, out AIFunction? function)
    {
        if (string.IsNullOrEmpty(name))
        {
            function = null;
            return false;
        }

        // Try to get the original function from the inner factory
        if (_innerFactory.TryFindTool(name, out var originalFunction) && originalFunction != null)
        {
            // Use CreateReplayWrapper to wrap it with replay functionality if appropriate
            function = _replayCore.CreateReplayWrapper(name, originalFunction);
            return true;
        }

        function = null;
        return false;
    }    /// <summary>
    /// Checks if a tool with the given name exists, either in replay data or in the inner factory.
    /// </summary>
    public bool HasTool(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        // Check if we have replay data for this function
        if (_replayCore.HasReplayDataForFunction(name))
        {
            return true;
        }

        // Fall back to checking the inner factory
        return _innerFactory.HasTool(name);
    }

    public void CheckForReplayFailures()
    {
        if (FunctionCallsWithReplayFailure.Count > 0)
        {
            throw new ReplayFailureException(GenerateReport());
        }
    }

    public string GenerateReport()
    {
        if (FunctionCallsWithReplayFailure.Count > 0)
        {
            var builder = new StringBuilder();
            builder.AppendLine("There were tool calls where replay was expected but no matching result was found in the imported logs:");
            foreach (var entry in FunctionCallsWithReplayFailure)
            {
                builder.AppendLine($"Function: {entry.FunctionName}, Args:");
                builder.AppendLine($"Args: {entry.FunctionArgumentsJson}");
            }
            return builder.ToString();
        }

        return "No tool calls with replay failure found.";
    }

    public List<ToolInfo> FetchAvailableToolInfo()
    {
        return _innerFactory.FetchAvailableToolInfo();
    }
}
