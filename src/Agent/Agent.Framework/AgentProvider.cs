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
    private readonly IVariantAssigner _variantAssigner;
    private readonly ILogger<AgentProvider<TContext>> _logger;
    private readonly string _instanceId;

    // Cache: VariantCombinationKey → Agent Graph
    private readonly ConcurrentDictionary<VariantCombinationKey, Dictionary<string, Agent<TContext>>> _graphCache = new();

    // Cache: VariantCombinationKey → Active Variants (for telemetry)
    private readonly ConcurrentDictionary<VariantCombinationKey, IReadOnlyDictionary<string, Variant>> _variantCache = new();

    // Forced experiment variants from environment variable
    private readonly Dictionary<string, string> _forcedVariants = [];

    // Set of experiment IDs to force disable
    private readonly HashSet<string> _disabledExperiments = [];

    public AgentProvider(
        IAgentFactory<TContext> factory,
        IVariantAssigner variantAssigner,
        ILogger<AgentProvider<TContext>> logger,
        string? instanceId = null)
    {
        _factory = factory;
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

        // Parse force-disabled experiments from environment variable
        // Format: "experiment1;experiment2;experiment3"
        var forceDisabledExperiments = Environment.GetEnvironmentVariable(FrameworkConstants.ForceDisableExperimentsEnvVar);
        if (!string.IsNullOrEmpty(forceDisabledExperiments))
        {
            var experiments = forceDisabledExperiments.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var exp in experiments)
            {
                var experimentId = exp.Trim();
                if (!string.IsNullOrEmpty(experimentId))
                {
                    _disabledExperiments.Add(experimentId);
                    _logger.LogInternalInformation(
                        "Force-disabled experiment: {ExperimentId}",
                        experimentId);
                }
            }
        }

        // Parse forced experiment variants from environment variable
        // Format: "experiment1=variant1;experiment2=variant2"
        var forcedVariants = Environment.GetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar);
        if (!string.IsNullOrEmpty(forcedVariants))
        {
            var experiments = forcedVariants.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var exp in experiments)
            {
                var parts = exp.Split('=', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var experimentId = parts[0].Trim();
                    var variantName = parts[1].Trim();
                    _forcedVariants[experimentId] = variantName;
                    _logger.LogInternalInformation(
                        "Forced experiment variant: {ExperimentId} = {VariantName}",
                        experimentId,
                        variantName);
                }
            }
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
        var assignments = _factory.Experiments
            .Select(exp =>
            {
                // Skip if this experiment is force-disabled
                if (_disabledExperiments.Contains(exp.ExperimentId))
                {
                    return new AssignedVariant(exp.ExperimentId, "disabled", InExperiment: false);
                }

                // Check for forced variants first
                if (_forcedVariants.TryGetValue(exp.ExperimentId, out var forcedVariantName))
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

        foreach (var experiment in _factory.Experiments)
        {
            // Skip if this experiment is force-disabled
            if (_disabledExperiments.Contains(experiment.ExperimentId))
            {
                _logger.LogInternalInformation(
                    "Experiment {ExperimentId} is force-disabled. Skipping.",
                    experiment.ExperimentId);
                continue;
            }

            Variant? variantToApply = null;

            // Check for forced variants first
            if (_forcedVariants.TryGetValue(experiment.ExperimentId, out var forcedVariantName))
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
