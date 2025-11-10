// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Validation;
using Agent.Data.DataModels;

namespace Agent.Web.Validation;

/// <summary>
/// Default implementation of IExtendedAgentValidator.
/// Currently provides empty validation (all validations pass).
/// This can be extended in the future to add specific validation rules for each resource type.
/// </summary>
public class ExtendedAgentValidator : IExtendedAgentValidator
{
    private readonly ILogger<ExtendedAgentValidator> _logger;

    public ExtendedAgentValidator(ILogger<ExtendedAgentValidator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<AgentValidationResult> ValidateAgentAsync(AgentDocumentModel model)
    {
        _logger.LogDebug("Validating AgentDocumentModel: {AgentName}", model.Name);

        var result = new AgentValidationResult();

        ValidateResourceMetadata(model.Metadata, result);
        ValidateAgentSpec(model.Spec, result);

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<AgentValidationResult> ValidateToolAsync(ToolDocumentModel model)
    {
        _logger.LogDebug("Validating ToolDocumentModel: {ToolName}", model.Name);

        var result = new AgentValidationResult();

        ValidateResourceMetadata(model.Metadata, result);

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<AgentValidationResult> ValidateConnectorAsync(ConnectorDocumentModel model)
    {
        _logger.LogDebug("Validating ConnectorDocumentModel: {ConnectorName}", model.Name);

        var result = new AgentValidationResult();

        ValidateResourceMetadata(model.Metadata, result);

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<AgentValidationResult> ValidatePluginConfigAsync(PlugInConfigDocumentModel model)
    {
        _logger.LogDebug("Validating PlugInConfigDocumentModel: {PluginName}", model.Name);

        var result = new AgentValidationResult();

        ValidateResourceMetadata(model.Metadata, result);

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<AgentValidationResult> ValidateCommonPromptAsync(CommonPromptDocumentModel model)
    {
        _logger.LogDebug("Validating CommonPromptDocumentModel: {PromptName}", model.Name);

        var result = new AgentValidationResult();

        ValidateResourceMetadata(model.Metadata, result);

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<AgentValidationResult> ValidateCommonToolsListAsync(CommonToolsListDocumentModel model)
    {
        _logger.LogDebug("Validating CommonToolsListDocumentModel: {ListName}", model.Name);

        var result = new AgentValidationResult();

        ValidateResourceMetadata(model.Metadata, result);

        return Task.FromResult(result);
    }

    private void ValidateResourceMetadata(ResourceMetadata metadata, AgentValidationResult result)
    {

        if (string.IsNullOrEmpty(metadata.Id))
        {
            result.Errors.Add("Id must not be empty.");
        }
    }

    // Ported from src\Agent\Agent.Core\Validation\AgentValidationService.cs
    // Not reusing it directly because it does not validate against the model
    private void ValidateAgentSpec(AgentSpec spec, AgentValidationResult result)
    {
        if (string.IsNullOrEmpty(spec.Name))
        {
            result.Errors.Add("Agent name is required.");
        }

        if (spec.Name.Any(char.IsWhiteSpace))
        {
            result.Errors.Add($"Agent name '{spec.Name}' must not contain whitespace.");
        }

        if (string.IsNullOrWhiteSpace(spec.Instructions))
        {
            result.Errors.Add("Agent instructions (system_prompt) are required.");
        }
        else
        {
            if (spec.Instructions.Length < 50)
            {
                result.AddError("System prompt must be longer than 50 characters.");
            }
            else if (spec.Instructions.Length > 60000) // ~15k tokens
            {
                result.AddError("System prompt must be under 60000 characters.");
            }
        }

        // Validate temperature range
        if (spec.Temperature.HasValue && (spec.Temperature.Value < 0 || spec.Temperature.Value > 2))
        {
            result.AddError("Temperature must be between 0 and 2.");
        }

        // Validate max reflection count
        if (spec.MaxReflectionCount < 0)
        {
            result.AddError("Max reflection count cannot be negative.");
        }

        // Validate handoff description length
        if (!string.IsNullOrWhiteSpace(spec.HandoffDescription) && spec.HandoffDescription.Length > 500)
        {
            result.AddError("Handoff description must be under 500 characters.");
        }

        // Validate tool names
        if (spec.Tools?.Count > 0)
        {
            foreach (var tool in spec.Tools)
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
        if (spec.McpTools?.Count > 0)
        {
            foreach (var tool in spec.McpTools)
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
        if (spec.Handoffs?.Count > 0)
        {
            foreach (var handoff in spec.Handoffs)
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
        if (spec.CommonPrompts?.Count > 0)
        {
            foreach (var prompt in spec.CommonPrompts)
            {
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    result.AddError("Common prompt name cannot be empty.");
                }
            }
        }

        // Validate agents as tools
        if (spec.AgentsAsTools?.Count > 0)
        {
            foreach (var agentTool in spec.AgentsAsTools)
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
}

