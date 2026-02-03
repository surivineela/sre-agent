// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Services;
using Agent.Framework;
using Agent.Framework.Hooks;

namespace Agent.Cli.Validations;

public static class AgentDescriptorValidation
{
    /// <summary>
    /// Validates an agent descriptor without checking tool existence.
    /// This is the basic validation for YAML structure and basic constraints.
    /// </summary>
    public static void ValidateAgentDescriptor(IAgentDescriptor? agentDescriptor, out List<string> errors)
    {
        errors = [];
        ValidateBasicConstraints(agentDescriptor, errors);
    }

    /// <summary>
    /// Validates an agent descriptor with optional tool existence checking.
    /// </summary>
    /// <param name="agentDescriptor">The agent descriptor to validate</param>
    /// <param name="toolAvailabilityService">Service for checking tool existence (null to skip tool existence validation)</param>
    /// <param name="errors">List of validation errors</param>
    public static async Task ValidateAgentDescriptorAsync(IAgentDescriptor? agentDescriptor, ToolAvailabilityService? toolAvailabilityService, List<string> errors)
    {
        ValidateBasicConstraints(agentDescriptor, errors);

        // Check tool existence if service is provided and there are tools to check
        if (toolAvailabilityService != null && agentDescriptor?.Tools != null && agentDescriptor.Tools.Count > 0)
        {
            await ValidateToolExistenceAsync(agentDescriptor.Tools, toolAvailabilityService, errors);
        }

        // TODO: Add MCP tool existence validation if needed
    }

    /// <summary>
    /// Validates basic constraints without tool existence checking.
    /// </summary>
    private static void ValidateBasicConstraints(IAgentDescriptor? agentDescriptor, List<string> errors)
    {
        if (agentDescriptor is null)
        {
            errors.Add("Agent descriptor is null.");
            return;
        }

        if (string.IsNullOrEmpty(agentDescriptor.Name))
        {
            errors.Add($"Agent descriptor {agentDescriptor.GetType().Name} does not have a name.");
        }

        if (string.IsNullOrEmpty(agentDescriptor.Instructions))
        {
            errors.Add($"Agent descriptor {agentDescriptor.Name} does not have instructions.");
        }

        // Ensure tools is not null - convert to empty array if null
        if (agentDescriptor.Tools == null)
        {
            agentDescriptor.Tools = [];
        }
        else if (agentDescriptor.Tools.Count > 0)
        {
            foreach (var tool in agentDescriptor.Tools)
            {
                if (string.IsNullOrWhiteSpace(tool))
                {
                    errors.Add("Tool name cannot be empty.");
                }
                else if (tool.Any(char.IsWhiteSpace))
                {
                    errors.Add($"Tool name '{tool}' must not contain whitespace.");
                }
            }
        }

        // Ensure mcp tools is not null - convert to empty array if null
        if (agentDescriptor.McpTools == null)
        {
            agentDescriptor.McpTools = [];
        }
        else if (agentDescriptor.McpTools.Count > 0)
        {
            foreach (var tool in agentDescriptor.McpTools)
            {
                if (string.IsNullOrWhiteSpace(tool))
                {
                    errors.Add("MCP tool name cannot be empty.");
                }
                else if (tool.Any(char.IsWhiteSpace))
                {
                    errors.Add($"MCP tool name '{tool}' must not contain whitespace.");
                }
            }
        }

        // Validate name doesn't contain whitespace
        if (!string.IsNullOrEmpty(agentDescriptor.Name) && agentDescriptor.Name.Any(char.IsWhiteSpace))
        {
            errors.Add($"Agent name '{agentDescriptor.Name}' must not contain whitespace.");
        }

        // Ensure handoffs is not null - convert to empty array if null
        if (agentDescriptor.Handoffs == null)
        {
            agentDescriptor.Handoffs = [];
        }
        else if (agentDescriptor.Handoffs.Count > 0)
        {
            foreach (var handoff in agentDescriptor.Handoffs)
            {
                if (string.IsNullOrWhiteSpace(handoff))
                {
                    errors.Add("Handoff name cannot be empty.");
                }
                else if (handoff.Any(char.IsWhiteSpace))
                {
                    errors.Add($"Handoff name '{handoff}' must not contain whitespace.");
                }
            }
        }

        // Validate temperature range if provided
        if (agentDescriptor.Temperature.HasValue && (agentDescriptor.Temperature.Value < 0 || agentDescriptor.Temperature.Value > 2))
        {
            errors.Add("Temperature must be between 0 and 2.");
        }

        // Validate max reflection count
        if (agentDescriptor.MaxReflectionCount < 0)
        {
            errors.Add("Max reflection count cannot be negative.");
        }

        // Validate instructions length
        if (!string.IsNullOrEmpty(agentDescriptor.Instructions))
        {
            if (agentDescriptor.Instructions.Length < 50)
            {
                errors.Add("System prompt must be longer than 50 characters.");
            }
            else if (agentDescriptor.Instructions.Length > 60000) // ~15k tokens
            {
                errors.Add("System prompt must be under 60000 characters.");
            }
        }

        // Validate handoff description if provided
        if (!string.IsNullOrWhiteSpace(agentDescriptor.HandoffDescription) && agentDescriptor.HandoffDescription.Length > 500)
        {
            errors.Add("Handoff description must be under 500 characters.");
        }

        // Validate common prompts
        if (agentDescriptor.CommonPrompts != null)
        {
            foreach (var prompt in agentDescriptor.CommonPrompts)
            {
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    errors.Add("Common prompt name cannot be empty.");
                }
            }
        }

        // Validate allowed_skills if provided
        if (agentDescriptor.AllowedSkills != null && agentDescriptor.AllowedSkills.Count > 0)
        {
            foreach (var skillName in agentDescriptor.AllowedSkills)
            {
                if (string.IsNullOrWhiteSpace(skillName))
                {
                    errors.Add("Skill name in allowed_skills cannot be empty.");
                }
                else if (skillName.Any(char.IsWhiteSpace))
                {
                    errors.Add($"Skill name '{skillName}' in allowed_skills must not contain whitespace.");
                }
            }

            // Check for duplicates
            var duplicateSkills = agentDescriptor.AllowedSkills
                .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            foreach (var duplicate in duplicateSkills)
            {
                errors.Add($"Duplicate skill name '{duplicate}' found in allowed_skills.");
            }
        }

        // Validate agents as tools
        if (agentDescriptor.AgentsAsTools != null)
        {
            foreach (var agentTool in agentDescriptor.AgentsAsTools)
            {
                if (string.IsNullOrWhiteSpace(agentTool.AgentName))
                {
                    errors.Add("Agent name in agents_as_tools cannot be empty.");
                }
                if (string.IsNullOrWhiteSpace(agentTool.ToolName))
                {
                    errors.Add("Tool name in agents_as_tools cannot be empty.");
                }
                if (string.IsNullOrWhiteSpace(agentTool.ToolDescription))
                {
                    errors.Add("Tool description in agents_as_tools cannot be empty.");
                }
            }
        }

        // Validate hooks if present
        if (agentDescriptor.Hooks != null && agentDescriptor.Hooks.Count > 0)
        {
            ValidateHooks(agentDescriptor.Hooks, errors);
        }

        // Enhanced validation for handoffs (only if handoffs is not null)
        if (agentDescriptor.Handoffs != null && agentDescriptor.Handoffs.Count > 0)
        {
            ValidateHandoffTargets(agentDescriptor.Name, agentDescriptor.Handoffs, errors);
        }
    }

    /// <summary>
    /// Validates hook configuration.
    /// </summary>
    private static void ValidateHooks(Dictionary<string, List<HookDefinition>> hooks, List<string> errors)
    {
        var validEventTypes = Enum.GetNames<HookEventType>();

        foreach (var kvp in hooks)
        {
            // Validate event type
            if (!Enum.TryParse<HookEventType>(kvp.Key, ignoreCase: true, out _))
            {
                errors.Add($"Invalid hook event type '{kvp.Key}'. Valid types are: {string.Join(", ", validEventTypes)}");
                continue;
            }

            // Validate hook definitions
            if (kvp.Value == null || kvp.Value.Count == 0)
            {
                errors.Add($"Hook event '{kvp.Key}' has no hook definitions.");
                continue;
            }

            foreach (var hook in kvp.Value)
            {
                ValidateHookDefinition(kvp.Key, hook, errors);
            }
        }
    }

    /// <summary>
    /// Validates a single hook definition.
    /// </summary>
    private static void ValidateHookDefinition(string eventType, HookDefinition hook, List<string> errors)
    {
        // Validate prompt-based hooks have a prompt
        if (hook.Type == HookType.Prompt && string.IsNullOrWhiteSpace(hook.Prompt))
        {
            errors.Add($"Prompt hook in '{eventType}' must have a prompt defined.");
        }

        // Validate command-based hooks have a command
        if (hook.Type == HookType.Command && string.IsNullOrWhiteSpace(hook.Command))
        {
            errors.Add($"Command hook in '{eventType}' must have a command defined.");
        }

        // Validate timeout is reasonable
        if (hook.Timeout <= 0)
        {
            errors.Add($"Hook in '{eventType}' has invalid timeout ({hook.Timeout}). Timeout must be positive.");
        }
        else if (hook.Timeout > 300)
        {
            errors.Add($"Hook in '{eventType}' has excessive timeout ({hook.Timeout}s). Maximum is 300 seconds.");
        }
    }

    /// <summary>
    /// Enhanced validation for handoff targets including circular dependency detection.
    /// </summary>
    private static void ValidateHandoffTargets(string agentName, List<string> handoffs, List<string> errors)
    {
        // Validate for self-reference
        if (handoffs.Contains(agentName))
        {
            errors.Add($"Agent '{agentName}' cannot have a handoff to itself.");
        }

        // Validate for duplicates
        var duplicates = handoffs.GroupBy(h => h).Where(g => g.Count() > 1).Select(g => g.Key);
        foreach (var duplicate in duplicates)
        {
            errors.Add($"Duplicate handoff target '{duplicate}' found.");
        }

        // Note: Agent existence and circular dependency validation would require
        // access to other agent definitions, which should be done at the service level
        // when full agent context is available
    }



    /// <summary>
    /// Validates that all tools referenced by the agent exist either locally or on the remote server.
    /// </summary>
    private static async Task ValidateToolExistenceAsync(List<string> toolNames, ToolAvailabilityService toolAvailabilityService, List<string> errors)
    {
        try
        {
            var (localTools, remoteTools, remoteErrors) = await toolAvailabilityService.GetAvailableToolsAsync();
            var allAvailableTools = new HashSet<string>(localTools);
            foreach (var tool in remoteTools)
            {
                allAvailableTools.Add(tool);
            }

            var missingTools = new List<string>();

            foreach (var toolName in toolNames)
            {
                if (!allAvailableTools.Contains(toolName))
                {
                    missingTools.Add(toolName);
                }
            }

            if (missingTools.Count > 0)
            {
                errors.Add($"The following tools are not available locally or remotely: {string.Join(", ", missingTools)}");
            }

            // If there were remote errors but we couldn't get all tools, add a warning
            if (remoteErrors.Count > 0)
            {
                foreach (var error in remoteErrors)
                {
                    errors.Add($"Warning: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            // If we can't check remote tools (e.g., server not reachable), still validate local tools
            try
            {
                var localTools = toolAvailabilityService.GetLocalTools();
                var missingLocalTools = new List<string>();

                foreach (var toolName in toolNames)
                {
                    if (!localTools.Contains(toolName))
                    {
                        missingLocalTools.Add(toolName);
                    }
                }

                if (missingLocalTools.Count > 0)
                {
                    errors.Add($"Unable to verify remote tools (server may be unreachable: {ex.Message}). The following tools are missing locally: {string.Join(", ", missingLocalTools)}. Ensure these tools exist locally or verify server connectivity.");
                }
            }
            catch (Exception localEx)
            {
                errors.Add($"Unable to validate tool existence: {localEx.Message}");
            }
        }
    }
}
