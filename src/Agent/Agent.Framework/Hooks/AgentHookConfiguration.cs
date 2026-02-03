// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;

namespace Agent.Framework.Hooks;

/// <summary>
/// Hook configuration for an agent.
/// </summary>
public class AgentHookConfiguration
{
    /// <summary>
    /// Hooks organized by event type.
    /// Key is the event type name (e.g., "Stop"), value is list of hook definitions.
    /// Uses case-insensitive comparison so "stop", "Stop", and "STOP" are all equivalent.
    /// </summary>
    public Dictionary<string, List<HookDefinition>> Hooks { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets hooks for a specific event type.
    /// </summary>
    /// <param name="eventType">The event type to get hooks for.</param>
    /// <returns>List of hook definitions for the event, or empty list if none configured.</returns>
    public List<HookDefinition> GetHooksForEvent(HookEventType eventType)
    {
        var eventName = eventType.ToString();
        return Hooks.TryGetValue(eventName, out var hooks) ? hooks : [];
    }

    /// <summary>
    /// Gets hooks for a specific event type that match the given tool name.
    /// For events that support matchers (PostToolUse), filters hooks by tool name pattern.
    /// </summary>
    /// <param name="eventType">The event type to get hooks for.</param>
    /// <param name="toolName">The tool name to match against hook matchers.</param>
    /// <returns>List of matching hook definitions.</returns>
    public List<HookDefinition> GetMatchingHooksForTool(HookEventType eventType, string toolName)
    {
        var allHooks = GetHooksForEvent(eventType);

        if (allHooks.Count == 0)
        {
            return [];
        }

        // For events that don't use matchers, return all hooks
        if (eventType != HookEventType.PostToolUse)
        {
            return allHooks;
        }

        // Filter hooks by matcher pattern
        var matchingHooks = new List<HookDefinition>();
        foreach (var hook in allHooks)
        {
            if (MatchesTool(hook.Matcher, toolName))
            {
                matchingHooks.Add(hook);
            }
        }

        return matchingHooks;
    }

    /// <summary>
    /// Checks if any hooks are configured for the specified event type.
    /// </summary>
    /// <param name="eventType">The event type to check.</param>
    /// <returns>True if hooks exist for this event.</returns>
    public bool HasHooksForEvent(HookEventType eventType)
    {
        var eventName = eventType.ToString();
        return Hooks.TryGetValue(eventName, out var hooks) && hooks.Count > 0;
    }

    /// <summary>
    /// Checks if a tool name matches a matcher pattern.
    /// </summary>
    /// <param name="matcher">The matcher pattern (regex or "*" for all). Empty/null does NOT match any tools.</param>
    /// <param name="toolName">The tool name to check.</param>
    /// <returns>True if the tool name matches the pattern.</returns>
    private static bool MatchesTool(string? matcher, string toolName)
    {
        // Empty or null does NOT match any tools
        if (string.IsNullOrWhiteSpace(matcher))
        {
            return false;
        }

        // Only "*" matches all tools
        if (matcher == "*")
        {
            return true;
        }

        try
        {
            // Treat matcher as regex pattern (case-sensitive to match Claude behavior)
            return Regex.IsMatch(toolName, $"^({matcher})$", RegexOptions.None, TimeSpan.FromMilliseconds(100));
        }
        catch (RegexParseException)
        {
            // If regex is invalid, fall back to exact match
            return string.Equals(matcher, toolName, StringComparison.Ordinal);
        }
        catch (RegexMatchTimeoutException)
        {
            // On timeout, fall back to exact match
            return string.Equals(matcher, toolName, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Creates an empty configuration.
    /// </summary>
    public static AgentHookConfiguration Empty => new();

    /// <summary>
    /// Gets prompt-type Stop hooks.
    /// </summary>
    /// <returns>List of prompt-type Stop hooks, or empty list if none configured.</returns>
    public List<HookDefinition> GetPromptStopHooks()
    {
        return GetHooksForEvent(HookEventType.Stop)
            .Where(h => h.Type == HookType.Prompt)
            .ToList();
    }

    /// <summary>
    /// Gets command-type Stop hooks.
    /// </summary>
    /// <returns>List of command-type Stop hooks, or empty list if none configured.</returns>
    public List<HookDefinition> GetCommandStopHooks()
    {
        return GetHooksForEvent(HookEventType.Stop)
            .Where(h => h.Type == HookType.Command)
            .ToList();
    }

    /// <summary>
    /// Checks if any prompt-based Stop hooks are configured.
    /// </summary>
    /// <returns>True if at least one prompt-type Stop hook exists.</returns>
    public bool HasPromptBasedStopHooks()
    {
        return GetPromptStopHooks().Count > 0;
    }

    /// <summary>
    /// Checks if any command-based Stop hooks are configured.
    /// </summary>
    /// <returns>True if at least one command-type Stop hook exists.</returns>
    public bool HasCommandBasedStopHooks()
    {
        return GetCommandStopHooks().Count > 0;
    }

    /// <summary>
    /// Gets the effective maximum stop hook rejections from prompt-based hooks only.
    /// Command-type Stop hooks are excluded as they have no implicit rejection limit.
    /// Returns the maximum MaxRejections value from all prompt-type Stop hooks that specify it,
    /// or null if no prompt hooks specify a value.
    /// </summary>
    public int? GetMaxStopHookRejections()
    {
        var promptHooks = GetPromptStopHooks();
        if (promptHooks.Count == 0)
        {
            return null;
        }

        var maxValues = promptHooks
            .Where(h => h.MaxRejections.HasValue)
            .Select(h => h.MaxRejections!.Value)
            .ToList();

        return maxValues.Count > 0 ? maxValues.Max() : null;
    }
}
