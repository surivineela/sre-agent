// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Configuration;

public class AgentModeManager<TContext> : IAgentModeManager<TContext>
    where TContext : class
{
    private readonly ILogger<AgentModeManager<TContext>> _logger;
    private readonly Dictionary<string, IAgentModeConfigurator<TContext>> _configurators;
    private readonly IReadOnlyDictionary<string, IPromptDescriptor> _promptDescriptors;

    public IAgentModeConfigurator<TContext> DefaultModeConfigurator { get; }

    public AgentModeManager(
        IAgentModeConfigurator<TContext> defaultModeConfigurator,
        ILoggerFactory loggerFactory,
        IAgentFactory<TContext> agentFactory)
    {
        _logger = loggerFactory.CreateLogger<AgentModeManager<TContext>>();
        _promptDescriptors = agentFactory.PromptDescriptors;
        DefaultModeConfigurator = defaultModeConfigurator;

        // Initialize all available configurators
        _configurators = new Dictionary<string, IAgentModeConfigurator<TContext>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ReadOnly"] = new ReadOnlyAgentModeConfigurator<TContext>(loggerFactory.CreateLogger<ReadOnlyAgentModeConfigurator<TContext>>()),
            ["Autonomous"] = new AutonomousAgentModeConfigurator<TContext>(loggerFactory.CreateLogger<AutonomousAgentModeConfigurator<TContext>>()),
            ["Review"] = new DefaultAgentModeConfigurator<TContext>()
        };
    }

    public void ApplyModeToAgent(
        Agent<TContext> agent,
        string? agentMode)
    {
        var configurator = GetConfiguratorForMode(agentMode);

        // Clear any existing mode-specific prompt
        agent.Instructions.ClearModeSpecificPrompt();

        // Apply the new mode
        ApplyModeSpecificPrompt(agent, configurator, agentMode);

        _logger.LogInternalInformation("Applied mode {mode} to agent {agentName}",
            agentMode ?? "Default", agent.Name);
    }

    private IAgentModeConfigurator<TContext> GetConfiguratorForMode(string? agentMode)
    {
        if (string.IsNullOrEmpty(agentMode))
        {
            return DefaultModeConfigurator;
        }

        return _configurators.TryGetValue(agentMode, out var configurator)
            ? configurator
            : DefaultModeConfigurator;
    }

    private void ApplyModeSpecificPrompt(
        Agent<TContext> agent,
        IAgentModeConfigurator<TContext> configurator,
        string? agentMode)
    {
        if (!configurator.AppliesToMode(agentMode))
        {
            return;
        }

        string? promptKey = agentMode?.ToLowerInvariant();
        if (promptKey != null && _promptDescriptors.TryGetValue(promptKey, out var modePrompt))
        {
            agent.Instructions.AddModeSpecificPrompt(modePrompt.Prompt);
        }
    }

    public IEnumerable<string> GetAvailableModes()
    {
        return _configurators.Keys;
    }
}
