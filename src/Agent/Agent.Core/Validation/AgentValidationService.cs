// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Framework;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Core.Validation;

/// <summary>
/// Unified agent validation service that can be used by both client and server-side code.
/// Provides comprehensive validation for agent descriptors including structure, constraints, and dependencies.
/// </summary>
public class AgentValidationService
{
    private readonly IToolAvailabilityChecker? _toolChecker;

    public AgentValidationService(IToolAvailabilityChecker? toolChecker = null)
    {
        _toolChecker = toolChecker;
    }

    /// <summary>
    /// Validates an agent descriptor with comprehensive checks.
    /// </summary>
    /// <param name="agentDescriptor">The agent descriptor to validate</param>
    /// <param name="checkToolAvailability">Whether to check if referenced tools are available</param>
    /// <returns>Validation result with success status and any errors</returns>
    public async Task<ApiValidationResult> ValidateAgentAsync(IAgentDescriptor? agentDescriptor, bool checkToolAvailability = false)
    {
        var result = new ApiValidationResult();

        if (agentDescriptor == null)
        {
            result.AddError("Agent descriptor is null.");
            return result;
        }

        // Perform basic structure validation
        ValidateBasicStructure(agentDescriptor, result);

        // Perform constraint validation
        ValidateConstraints(agentDescriptor, result);

        // Perform tool availability validation if requested and service is available
        if (checkToolAvailability && _toolChecker != null && agentDescriptor.Tools?.Count > 0)
        {
            await ValidateToolAvailabilityAsync(agentDescriptor.Tools, result);
        }

        // TODO: Validate tool availability for MCP tools (Remote MCPs may not be always available)

        return result;
    }

    /// <summary>
    /// Validates YAML content and returns both parsing and validation results.
    /// </summary>
    /// <param name="yamlContent">The YAML content to validate</param>
    /// <param name="checkToolAvailability">Whether to check tool availability</param>
    /// <returns>Combined parsing and validation result</returns>
    public async Task<ApiValidationResult> ValidateYamlAsync(string yamlContent, bool checkToolAvailability = false)
    {
        var result = new ApiValidationResult();

        try
        {
            // First, validate basic YAML structure
            if (string.IsNullOrWhiteSpace(yamlContent))
            {
                result.AddError("YAML content is empty.");
                return result;
            }

            // Parse the YAML using the new structured format
            var agentDescriptor = ParseStructuredYaml(yamlContent);
            if (agentDescriptor == null)
            {
                result.AddError("Failed to parse YAML into a valid agent descriptor.");
                return result;
            }

            // Validate the parsed agent
            var validationResult = await ValidateAgentAsync(agentDescriptor, checkToolAvailability);
            result.MergeWith(validationResult);

            return result;
        }
        catch (YamlException yamlEx)
        {
            // Provide specific YAML syntax errors with line information
            var lineInfo = yamlEx.Start.Line > 0 ? $" at line {yamlEx.Start.Line}, column {yamlEx.Start.Column}" : "";
            result.AddError($"YAML syntax error{lineInfo}: {yamlEx.Message}");
            return result;
        }
        catch (InvalidDataException dataEx)
        {
            // Handle schema validation errors
            result.AddError($"Invalid YAML structure: {dataEx.Message}");
            return result;
        }
        catch (Exception ex)
        {
            // Generic fallback with better context
            result.AddError($"YAML parsing failed: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Validates basic agent structure and required fields.
    /// </summary>
    private static void ValidateBasicStructure(IAgentDescriptor agent, ApiValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(agent.Name))
        {
            result.AddError("Agent name is required.");
        }
        else if (agent.Name.Any(char.IsWhiteSpace))
        {
            result.AddError($"Agent name '{agent.Name}' must not contain whitespace.");
        }

        if (string.IsNullOrWhiteSpace(agent.Instructions))
        {
            result.AddError("Agent instructions (system_prompt) are required.");
        }
    }

    /// <summary>
    /// Validates agent constraints and business rules.
    /// </summary>
    private static void ValidateConstraints(IAgentDescriptor agent, ApiValidationResult result)
    {
        // Validate instructions length
        if (!string.IsNullOrWhiteSpace(agent.Instructions))
        {
            if (agent.Instructions.Length < 50)
            {
                result.AddError("System prompt must be longer than 50 characters.");
            }
            else if (agent.Instructions.Length > 60000) // ~15k tokens
            {
                result.AddError("System prompt must be under 60000 characters.");
            }
        }

        // Validate temperature range
        if (agent.Temperature.HasValue && (agent.Temperature.Value < 0 || agent.Temperature.Value > 2))
        {
            result.AddError("Temperature must be between 0 and 2.");
        }

        // Validate max reflection count
        if (agent.MaxReflectionCount < 0)
        {
            result.AddError("Max reflection count cannot be negative.");
        }

        // Validate handoff description length
        if (!string.IsNullOrWhiteSpace(agent.HandoffDescription) && agent.HandoffDescription.Length > 500)
        {
            result.AddError("Handoff description must be under 500 characters.");
        }

        // Validate tool names
        if (agent.Tools?.Count > 0)
        {
            foreach (var tool in agent.Tools)
            {
                if (string.IsNullOrWhiteSpace(tool))
                {
                    result.AddError("Tool name cannot be empty.");
                }
                else if (tool.Any(char.IsWhiteSpace))
                {
                    result.AddError($"Tool name '{tool}' must not contain whitespace.");
                }
            }
        }

        // Validate MCP tool names
        if (agent.McpTools?.Count > 0)
        {
            foreach (var tool in agent.McpTools)
            {
                if (string.IsNullOrWhiteSpace(tool))
                {
                    result.AddError("MCP tool name cannot be empty.");
                }
                else if (tool.Any(char.IsWhiteSpace))
                {
                    result.AddError($"MCP tool name '{tool}' must not contain whitespace.");
                }
            }
        }

        // Validate handoff names
        if (agent.Handoffs?.Count > 0)
        {
            foreach (var handoff in agent.Handoffs)
            {
                if (string.IsNullOrWhiteSpace(handoff))
                {
                    result.AddError("Handoff name cannot be empty.");
                }
                else if (handoff.Any(char.IsWhiteSpace))
                {
                    result.AddError($"Handoff name '{handoff}' must not contain whitespace.");
                }
            }
        }

        // Validate common prompts
        if (agent.CommonPrompts?.Count > 0)
        {
            foreach (var prompt in agent.CommonPrompts)
            {
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    result.AddError("Common prompt name cannot be empty.");
                }
            }
        }

        // Validate agents as tools
        if (agent.AgentsAsTools?.Count > 0)
        {
            foreach (var agentTool in agent.AgentsAsTools)
            {
                if (string.IsNullOrWhiteSpace(agentTool.AgentName))
                {
                    result.AddError("Agent name in agents_as_tools cannot be empty.");
                }
                if (string.IsNullOrWhiteSpace(agentTool.ToolName))
                {
                    result.AddError("Tool name in agents_as_tools cannot be empty.");
                }
                if (string.IsNullOrWhiteSpace(agentTool.ToolDescription))
                {
                    result.AddError("Tool description in agents_as_tools cannot be empty.");
                }
            }
        }
    }

    /// <summary>
    /// Validates that all referenced tools are available.
    /// </summary>
    private async Task ValidateToolAvailabilityAsync(List<string> toolNames, ApiValidationResult result)
    {
        if (_toolChecker == null)
        {
            result.AddWarning("Tool availability checker not configured. Skipping tool validation.");
            return;
        }

        try
        {
            var availabilityResult = await _toolChecker.CheckToolsAvailabilityAsync(toolNames);

            if (availabilityResult.MissingTools.Count > 0)
            {
                result.AddError($"The following tools are not available: {string.Join(", ", availabilityResult.MissingTools)}");
            }

            // Add warnings for any issues encountered during tool checking
            foreach (var warning in availabilityResult.Warnings)
            {
                result.AddWarning(warning);
            }
        }
        catch (Exception ex)
        {
            result.AddWarning($"Unable to validate tool availability: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses the new structured YAML format (api_version, kind, spec) into an IAgentDescriptor.
    /// </summary>
    private static IAgentDescriptor? ParseStructuredYaml(string yamlContent)
    {
        try
        {
            // Use the same deserialization logic as the web API
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var yamlObject = deserializer.Deserialize(new StringReader(yamlContent));
            var jsonString = JsonSerializer.Serialize(yamlObject);
            var generic = JsonSerializer.Deserialize<GenericResourceModel>(jsonString);

            if (generic == null || string.IsNullOrEmpty(generic.Kind))
            {
                throw new InvalidOperationException("YAML content must contain 'kind' field");
            }

            if (!generic.Kind.Equals("AgentConfiguration", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Expected kind 'AgentConfiguration', but got '{generic.Kind}'");
            }

            // Deserialize as structured YAML model
            var structuredYaml = deserializer.Deserialize<StructuredAgentYaml>(yamlContent);

            if (structuredYaml?.Spec == null)
            {
                throw new InvalidOperationException("YAML content must contain 'spec' field");
            }

            // Return the spec directly as it implements IAgentDescriptor
            return structuredYaml.Spec;
        }
        catch (YamlException)
        {
            // Let YamlException bubble up for better error handling
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse YAML: {ex.Message}", ex);
        }
    }

}

/// <summary>
/// Simple model for parsing structured YAML with api_version, kind, and spec.
/// </summary>
public class StructuredAgentYaml
{
    [YamlMember(Alias = "api_version")]
    public string ApiVersion { get; set; } = string.Empty;

    [YamlMember(Alias = "kind")]
    public string Kind { get; set; } = string.Empty;

    [YamlMember(Alias = "spec")]
    public YamlAgentDescriptor Spec { get; set; } = new();
}

/// <summary>
/// Generic resource model for initial YAML structure validation.
/// </summary>
public class GenericResourceModel
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;
}

/// <summary>
/// Result of agent validation containing success status and any errors/warnings.
/// </summary>
public class ApiValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    public void AddError(string error)
    {
        Errors.Add(error);
    }

    public void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }

    public void MergeWith(ApiValidationResult other)
    {
        Errors.AddRange(other.Errors);
        Warnings.AddRange(other.Warnings);
    }

    public override string ToString()
    {
        var messages = new List<string>();

        if (Errors.Count > 0)
        {
            messages.Add($"Errors: \n{string.Join("\n", Errors)}");
        }

        if (Warnings.Count > 0)
        {
            messages.Add($"Warnings: \n{string.Join("\n", Warnings)}");
        }

        return messages.Count > 0 ? string.Join("\n\n", messages) : "Valid";
    }
}

/// <summary>
/// Interface for checking tool availability. Can be implemented by both client and server.
/// </summary>
public interface IToolAvailabilityChecker
{
    Task<ToolAvailabilityResult> CheckToolsAvailabilityAsync(List<string> toolNames);
}

/// <summary>
/// Result of tool availability check.
/// </summary>
public class ToolAvailabilityResult
{
    public List<string> AvailableTools { get; } = new();
    public List<string> MissingTools { get; } = new();
    public List<string> Warnings { get; } = new();
}
