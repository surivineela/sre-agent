// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using Agent.Framework;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;

namespace Agent.Runtime;

public class AgentRuntimeModifier : IAgentRuntimeModifier<AgentContext>
{
    private readonly ConcurrentDictionary<string, AgentRuntimeModifications> _agentModifications = new();
    private readonly ILogger<AgentRuntimeModifier> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IThreadRepository _threadRepository;

    private readonly ActionSettings _actionSettings;

    public AgentRuntimeModifier(
        ILogger<AgentRuntimeModifier> logger,
        IServiceProvider serviceProvider,
        ActionSettings actionSettings,
        IThreadRepository threadRepository)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _actionSettings = actionSettings;
        _threadRepository = threadRepository;
    }

    /// <summary>
    /// Applies runtime modifications to the agent before each turn.
    /// </summary>
    /// <returns>A ChatMessage explaining the agent mode change if mode was changed, null otherwise</returns>
    public Task<ChatMessage?> ApplyRuntimeModificationsAsync(RunContextWrapper<AgentContext> contextWrapper, Agent<AgentContext> agent, CancellationToken cancellationToken = default)
    {
        if (contextWrapper?.Context == null)
        {
            _logger.LogInternalWarning("Context is null, cannot apply runtime modifications");
            return Task.FromResult<ChatMessage?>(null);
        }

        var threadId = contextWrapper.Context.ThreadId;
        _logger.LogInternalInformation("Applying runtime modifications to agent {AgentName} for thread {ThreadId}", agent.Name, threadId);

        if (_agentModifications.TryGetValue(threadId.ToString(), out var modifications))
        {
            _logger.LogInternalInformation("Applying runtime modifications to agent {AgentName} for thread {ThreadId}", agent.Name, threadId);

            // Apply agent mode using IAgentModeManager
            if (!string.IsNullOrEmpty(modifications.AgentMode))
            {
                // Get the correctly typed agent mode manager from service provider
                var agentModeManager = _serviceProvider.GetService<IAgentModeManager<AgentContext>>();
                if (agentModeManager != null)
                {
                    agentModeManager.ApplyModeToAgent(agent, modifications.AgentMode);
                    _logger.LogInternalInformation("Applied agent mode {AgentMode} to agent {AgentName} for thread {ThreadId}", modifications.AgentMode, agent.Name, threadId);

                    // Return a ChatMessage with the agent mode explanation
                    var modeExplanation = GetAgentModeExplanation(modifications.AgentMode);
                    return Task.FromResult<ChatMessage?>(new ChatMessage(ChatRole.System, modeExplanation));
                }
                else
                {
                    _logger.LogInternalInformation("No IAgentModeManager<AgentContext> found for agent {AgentName}", agent.Name);
                }
            }
        }

        return Task.FromResult<ChatMessage?>(null);
    }

    public async Task SetAgentMode(AgentContext context, string? agentMode, bool notifyUser = true)
    {
        var threadId = context.ThreadId;
        _agentModifications.AddOrUpdate(threadId.ToString(),
            new AgentRuntimeModifications { AgentMode = agentMode },
            (key, existing) =>
            {
                existing.AgentMode = agentMode;
                existing.LastModified = DateTime.UtcNow;
                return existing;
            });

        _logger.LogInternalInformation("Set agent mode to {AgentMode} for thread {ThreadId}", agentMode ?? "Default", threadId);

        // Add notification message to the thread only if notifyUser is true
        if (notifyUser)
        {
            try
            {
                var modeDisplayName = agentMode ?? "Default";
                var notificationMessage = new Message(
                    Id: Guid.NewGuid(),
                    TimeStamp: DateTime.UtcNow,
                    Author: new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
                    IsImageContent: false,
                    Text: $"Agent mode has been changed to: **{modeDisplayName}**",
                    Posted: new Posted(false),
                    Approval: null
                );

                await _threadRepository.AddMessageAsync(threadId, notificationMessage);
                _logger.LogInternalInformation("Added agent mode change notification message for thread {ThreadId} to user", threadId);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError("Error adding agent mode change notification message: {Message}", ex.Message);
                // Don't throw here as the mode change itself was successful
            }
        }
        else
        {
            _logger.LogInternalInformation("Skipped user notification for agent mode change on thread {ThreadId}", threadId);
        }

    }

    public void ClearAgentModifications(AgentContext context)
    {
        var threadId = context.ThreadId;
        _agentModifications.TryRemove(threadId.ToString(), out _);
        _logger.LogInternalInformation("Cleared all modifications for thread {ThreadId}", threadId);
    }

    public AgentRuntimeModifications? GetAgentModifications(AgentContext context)
    {
        var threadId = context.ThreadId;
        return _agentModifications.TryGetValue(threadId.ToString(), out var modifications) ? modifications : null;
    }

    public string GetThreadAgentMode(AgentContext context)
    {
        var threadId = context.ThreadId;
        if (_agentModifications.TryGetValue(threadId.ToString(), out var modifications) && !string.IsNullOrEmpty(modifications.AgentMode))
        {
            return modifications.AgentMode;
        }

        return _actionSettings.Mode.ToString() ?? "Review";
    }

    private static string GetAgentModeExplanation(string? agentMode)
    {
        var modeDisplayName = agentMode ?? "Default (Review)";
        var normalizedMode = agentMode?.ToLowerInvariant();

        return normalizedMode switch
        {
            "readonly" =>
                $"AGENT MODE CHANGE: The agent mode has been changed to **{modeDisplayName}**.\n\n" +
                "This means you are now operating in Read-Only mode. In this mode:\n" +
                "- You can only perform READ operations and data analysis\n" +
                "- You CANNOT execute any write, modify, or destructive actions\n" +
                "- You should focus on investigation, monitoring, and providing recommendations\n" +
                "- Any suggested actions should be clearly marked as recommendations only\n" +
                "Please adjust your behavior accordingly for all subsequent interactions.",

            "autonomous" =>
                $"AGENT MODE CHANGE: The agent mode has been changed to **{modeDisplayName}**.\n\n" +
                "This means you are now operating in Autonomous mode. In this mode:\n" +
                "- You can execute actions automatically without requiring user approval\n" +
                "- You have full permissions to make changes and take corrective actions\n" +
                "- You should be proactive in identifying and resolving issues\n" +
                "- Exercise appropriate caution but act decisively when needed\n" +
                "Please adjust your behavior accordingly for all subsequent interactions.",

            "review" or _ =>
                $"AGENT MODE CHANGE: The agent mode has been changed to **{modeDisplayName}**.\n\n" +
                "This is the default Review mode. In this mode:\n" +
                "- You can execute actions directly as the system has built-in approval hooks that handle user consent\n" +
                "- You don't need to explicitly ask for user approval before taking actions\n" +
                "- You should present clear action plans with rationale and then proceed with execution\n" +
                "- Focus on providing detailed explanations of your actions and their expected outcomes\n" +
                "Please adjust your behavior accordingly for all subsequent interactions."
        };
    }
}
