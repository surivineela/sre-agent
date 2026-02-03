// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace Agent.Framework.Hooks;

/// <summary>
/// Manages hook execution for agents and orchestrates hook execution for different events.
/// </summary>
public class HookManager
{
    private readonly IEnumerable<IHookExecutor> _executors;
    private readonly ILogger<HookManager> _logger;
    private readonly bool _enabled;

    public HookManager(
        IEnumerable<IHookExecutor> executors,
        ILogger<HookManager> logger,
        bool enabled = true)
    {
        _executors = executors;
        _logger = logger;
        _enabled = enabled;
    }

    /// <summary>
    /// Whether hooks are enabled globally.
    /// </summary>
    public bool Enabled => _enabled;

    /// <summary>
    /// Executes all hooks for a given event and returns the aggregated result.
    /// If any hook returns ok=false, the overall result is a rejection.
    /// Returns success immediately if hooks are disabled globally.
    /// </summary>
    /// <param name="configuration">The agent's hook configuration.</param>
    /// <param name="eventType">The event type to execute hooks for.</param>
    /// <param name="context">Context for the hook execution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated hook result. Ok=false if any hook rejected.</returns>
    public async Task<HookResult> ExecuteHooksAsync(
        AgentHookConfiguration? configuration,
        HookEventType eventType,
        HookContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_enabled)
        {
            _logger.LogDebug("Hooks are disabled globally, skipping execution");
            return HookResult.Success();
        }

        if (configuration == null || !configuration.HasHooksForEvent(eventType))
        {
            _logger.LogDebug("No hooks configured for event {EventType}", eventType);
            return HookResult.Success();
        }

        var hooks = configuration.GetHooksForEvent(eventType);
        _logger.LogDebug("Executing {Count} hook(s) for event {EventType}", hooks.Count, eventType);

        return await ExecuteHookListInternalAsync(hooks, context, cancellationToken);
    }

    /// <summary>
    /// Executes hooks for a tool-related event, filtering by tool name matcher.
    /// </summary>
    /// <param name="configuration">The agent's hook configuration.</param>
    /// <param name="eventType">The event type to execute hooks for.</param>
    /// <param name="toolName">The tool name to match against hook matchers.</param>
    /// <param name="context">Context for the hook execution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated hook result. Ok=false if any hook rejected.</returns>
    public async Task<HookResult> ExecuteHooksForToolAsync(
        AgentHookConfiguration? configuration,
        HookEventType eventType,
        string toolName,
        HookContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_enabled)
        {
            _logger.LogDebug("Hooks are disabled globally, skipping execution");
            return HookResult.Success();
        }

        if (configuration == null || !configuration.HasHooksForEvent(eventType))
        {
            _logger.LogDebug("No hooks configured for event {EventType}", eventType);
            return HookResult.Success();
        }

        var hooks = configuration.GetMatchingHooksForTool(eventType, toolName);
        if (hooks.Count == 0)
        {
            _logger.LogDebug("No hooks match tool {ToolName} for event {EventType}", toolName, eventType);
            return HookResult.Success();
        }

        _logger.LogDebug("Executing {Count} hook(s) for event {EventType} on tool {ToolName}",
            hooks.Count, eventType, toolName);

        return await ExecuteHookListInternalAsync(hooks, context, cancellationToken);
    }

    /// <summary>
    /// Executes a specific list of hooks and returns the aggregated result.
    /// Use this method to execute a subset of hooks (e.g., only prompt hooks or only command hooks).
    /// </summary>
    /// <param name="hooks">The list of hooks to execute.</param>
    /// <param name="context">Context for the hook execution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated hook result. Ok=false if any hook rejected.</returns>
    public async Task<HookResult> ExecuteHookListAsync(
        List<HookDefinition> hooks,
        HookContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_enabled || hooks.Count == 0)
        {
            return HookResult.Success();
        }

        return await ExecuteHookListInternalAsync(hooks, context, cancellationToken);
    }

    /// <summary>
    /// Executes a list of hooks and aggregates results.
    /// </summary>
    private async Task<HookResult> ExecuteHookListInternalAsync(
        List<HookDefinition> hooks,
        HookContext context,
        CancellationToken cancellationToken)
    {
        var rejectionReasons = new List<string>();
        string? additionalContext = null;

        foreach (var hook in hooks)
        {
            var executor = GetExecutor(hook.Type);
            if (executor == null)
            {
                _logger.LogWarning("No executor found for hook type {HookType}, skipping", hook.Type);
                continue;
            }

            try
            {
                var result = await executor.ExecuteAsync(hook, context, cancellationToken);

                // Capture additionalContext from any hook (last one wins if multiple)
                var hookContext = result.GetAdditionalContext();
                if (!string.IsNullOrWhiteSpace(hookContext))
                {
                    additionalContext = hookContext;
                }

                if (!result.Ok)
                {
                    _logger.LogInformation("Hook rejected action: {Reason}", result.Reason);
                    if (!string.IsNullOrWhiteSpace(result.Reason))
                    {
                        rejectionReasons.Add(result.Reason);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hook execution failed, continuing with next hook");
            }
        }

        if (rejectionReasons.Count > 0)
        {
            var combinedReason = string.Join("\n", rejectionReasons);
            return additionalContext != null
                ? HookResult.RejectWithContext(combinedReason, additionalContext)
                : HookResult.Reject(combinedReason);
        }

        return additionalContext != null
            ? HookResult.SuccessWithContext(additionalContext)
            : HookResult.Success();
    }

    /// <summary>
    /// Creates an AgentHookConfiguration from a dictionary (as parsed from YAML).
    /// Validates that all keys are valid HookEventType values.
    /// </summary>
    /// <param name="hooks">Dictionary of event name to hook definitions.</param>
    /// <returns>Agent hook configuration.</returns>
    /// <exception cref="ArgumentException">Thrown when an invalid hook event type is specified.</exception>
    public static AgentHookConfiguration CreateFromDictionary(Dictionary<string, List<HookDefinition>>? hooks)
    {
        if (hooks == null || hooks.Count == 0)
        {
            return AgentHookConfiguration.Empty;
        }

        // Validate keys and create case-insensitive dictionary
        var validatedHooks = new Dictionary<string, List<HookDefinition>>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in hooks)
        {
            if (!Enum.TryParse<HookEventType>(kvp.Key, ignoreCase: true, out var eventType))
            {
                var validTypes = string.Join(", ", Enum.GetNames<HookEventType>());
                throw new ArgumentException(
                    $"Invalid hook event type '{kvp.Key}'. Valid types are: {validTypes}",
                    nameof(hooks));
            }

            // Validate MaxRejections for each hook
            foreach (var hook in kvp.Value)
            {
                if (hook.MaxRejections is not null && (hook.MaxRejections < 1 || hook.MaxRejections > 25))
                {
                    throw new ArgumentException(
                        $"Hook in event '{kvp.Key}' has invalid MaxRejections value ({hook.MaxRejections}). Valid range is 1-25.",
                        nameof(hooks));
                }
            }

            // Use the canonical enum name as the key
            validatedHooks[eventType.ToString()] = kvp.Value;
        }

        return new AgentHookConfiguration
        {
            Hooks = validatedHooks
        };
    }

    private IHookExecutor? GetExecutor(HookType type)
    {
        return _executors.FirstOrDefault(e => e.SupportedType == type);
    }
}
