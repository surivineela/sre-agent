// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Agent.Framework;

/// <summary>
/// Provides agents with experiment variants applied based on thread context.
/// Caches agent graphs by variant combination for efficiency.
/// </summary>
public sealed class AgentProvider<TContext> : IAgentProvider<TContext>
    where TContext : class
{
    private readonly IAgentFactory<TContext> _factory;
    private readonly IExperimentLoader _experimentLoader;
    private readonly IVariantAssigner _variantAssigner;
    private readonly ILogger<AgentProvider<TContext>> _logger;
    private readonly string _instanceId;

    // Cache: VariantCombinationKey → Agent Graph
    private readonly ConcurrentDictionary<VariantCombinationKey, Dictionary<string, Agent<TContext>>> _graphCache = new();

    // Cache: VariantCombinationKey → Active Variants (for telemetry)
    private readonly ConcurrentDictionary<VariantCombinationKey, IReadOnlyDictionary<string, Variant>> _variantCache = new();

    public AgentProvider(
        IAgentFactory<TContext> factory,
        IExperimentLoader experimentLoader,
        IVariantAssigner variantAssigner,
        ILogger<AgentProvider<TContext>> logger,
        string? instanceId = null)
    {
        _factory = factory;
        _experimentLoader = experimentLoader;
        _variantAssigner = variantAssigner;
        _logger = logger;
        _instanceId = instanceId ?? "default";

        // Subscribe to agent changes to automatically invalidate cache
        if (_factory is AgentFactory<TContext> concreteFactory)
        {
            concreteFactory.AgentChanged += OnAgentChanged;
            _logger.LogInternalInformation("AgentProvider subscribed to AgentFactory.AgentChanged event");
        }
        else
        {
            _logger.LogInternalWarning("AgentProvider could not subscribe to AgentChanged event - factory is not AgentFactory<TContext>");
        }
    }

    public Agent<TContext> GetAgent(string name, string? threadId = null)
    {
        var key = GetOrCreateVariantKey(threadId);
        var agentGraph = _graphCache.GetOrAdd(key, _ => CreateAgentGraph(threadId, key));

        if (!agentGraph.TryGetValue(name, out var agent))
        {
            throw new KeyNotFoundException($"Agent {name} not found.");
        }

        return agent;
    }

    public IReadOnlyDictionary<string, Variant> GetActiveVariants(string? threadId = null)
    {
        var key = GetOrCreateVariantKey(threadId);
        if (_variantCache.TryGetValue(key, out var variants))
        {
            return variants;
        }

        // Force graph creation if needed (this populates both caches)
        _ = _graphCache.GetOrAdd(key, _ => CreateAgentGraph(threadId, key));
        return _variantCache[key];
    }

    private VariantCombinationKey GetOrCreateVariantKey(string? threadId)
    {
        var assignments = _experimentLoader.Experiments
            .Select(exp =>
            {
                // Skip if this experiment is force-disabled
                if (_experimentLoader.IsExperimentDisabled(exp.ExperimentId))
                {
                    return new AssignedVariant(exp.ExperimentId, "disabled", InExperiment: false);
                }

                // Check for forced variants first
                var forcedVariantName = _experimentLoader.GetForcedVariant(exp.ExperimentId);
                if (forcedVariantName != null)
                {
                    var forcedVariant = exp.Variants.FirstOrDefault(v => v.Name == forcedVariantName);
                    if (forcedVariant != null)
                    {
                        return new AssignedVariant(exp.ExperimentId, forcedVariantName, InExperiment: true);
                    }
                }

                // Normal assignment if not forced
                return _variantAssigner.Assign(exp, _instanceId, threadId);
            })
            .ToList();

        return new VariantCombinationKey(assignments);
    }

    private Dictionary<string, Agent<TContext>> CreateAgentGraph(string? threadId, VariantCombinationKey key)
    {
        _logger.LogInternalInformation(
            "Creating new agent graph for threadId: {ThreadId}, variant combination: {VariantKey}",
            threadId ?? "global",
            key);

        // 1. Get base agents and cast factory to access implementation-specific members
        var factory = (AgentFactory<TContext>)_factory;
        var baseAgents = factory.GetAllAgents();

        // 2. Clone the agent graph
        var clonedGraph = AgentGraphCloner.Clone(
            baseAgents,
            enableHandoffReasoning: factory.EnableHandoffReasoning);

        // 3. Assign variants and apply overlays
        var activeVariants = new Dictionary<string, Variant>();

        foreach (var experiment in _experimentLoader.Experiments)
        {
            // Skip if this experiment is force-disabled
            if (_experimentLoader.IsExperimentDisabled(experiment.ExperimentId))
            {
                _logger.LogInternalInformation(
                    "Experiment {ExperimentId} is force-disabled. Skipping.",
                    experiment.ExperimentId);
                continue;
            }

            Variant? variantToApply = null;

            // Check for forced variants first
            var forcedVariantName = _experimentLoader.GetForcedVariant(experiment.ExperimentId);
            if (forcedVariantName != null)
            {
                variantToApply = experiment.Variants.FirstOrDefault(v => v.Name == forcedVariantName);
                if (variantToApply != null)
                {
                    _logger.LogInternalInformation(
                        "Applying forced experiment {ExperimentId} variant {VariantName} for threadId: {ThreadId}",
                        experiment.ExperimentId,
                        forcedVariantName,
                        threadId ?? "global");
                }
                else
                {
                    _logger.LogInternalWarning(
                        "Experiment {ExperimentId} does not have a variant named {VariantName} to force. Skipping.",
                        experiment.ExperimentId,
                        forcedVariantName);
                    continue;
                }
            }
            else
            {
                // Normal assignment if not forced
                if (!experiment.Enabled)
                {
                    continue;
                }

                var assignment = _variantAssigner.Assign(experiment, _instanceId, threadId);

                if (!assignment.InExperiment)
                {
                    continue;
                }

                variantToApply = experiment.Variants.First(v => v.Name == assignment.VariantName);

                _logger.LogInternalInformation(
                    "Applied experiment {ExperimentId} variant {VariantName} for threadId: {ThreadId}",
                    experiment.ExperimentId,
                    variantToApply.Name,
                    threadId ?? "global");
            }

            // Apply the selected variant overlay
            factory.ApplyVariantOverlayToGraph(clonedGraph, variantToApply.Overlay);
            activeVariants[experiment.ExperimentId] = variantToApply;
        }

        // Cache active variants for telemetry
        _variantCache[key] = activeVariants.AsReadOnly();

        return clonedGraph;
    }

    /// <summary>
    /// Event handler for agent changes from AgentFactory
    /// </summary>
    private void OnAgentChanged(object? sender, AgentChangedEventArgs e)
    {
        _logger.LogInternalInformation(
            "Agent '{AgentName}' was {ChangeType} - invalidating agent graph cache",
            e.AgentName,
            e.ChangeType);

        InvalidateCache();
    }

    /// <summary>
    /// Invalidates all cached agent graphs, forcing them to be rebuilt on next access
    /// </summary>
    private void InvalidateCache()
    {
        var graphCount = _graphCache.Count;
        var variantCount = _variantCache.Count;

        _graphCache.Clear();
        _variantCache.Clear();

        _logger.LogInternalInformation(
            "Agent graph cache invalidated - cleared {GraphCount} cached graphs and {VariantCount} variant entries",
            graphCount,
            variantCount);
    }
}
