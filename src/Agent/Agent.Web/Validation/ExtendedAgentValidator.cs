// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Validation;
using Agent.Data.DataModels;
using Agent.Web.Services;

namespace Agent.Web.Validation;

/// <summary>
/// Default implementation of IExtendedAgentValidator.
/// Currently provides empty validation (all validations pass).
/// This can be extended in the future to add specific validation rules for each resource type.
/// </summary>
public class ExtendedAgentValidator : IExtendedAgentValidator
{
    private readonly ILogger<ExtendedAgentValidator> _logger;
    private readonly IExtendedAgentRepository _repository;

    // use a predefined set to control which system common prompts we'd like to expose
    private readonly HashSet<string> _systemCommonPrompts = new HashSet<string>
    {
    };

    public ExtendedAgentValidator(ILogger<ExtendedAgentValidator> logger, IExtendedAgentRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    /// <inheritdoc/>
    public async Task<ApiValidationResult> ValidateAgentAsync(AgentDocumentModel model)
    {
        _logger.LogDebug("Validating AgentDocumentModel: {AgentName}", model.Name);

        var result = new ApiValidationResult();

        ValidateResourceMetadata(model.Metadata, result);
        await ValidateAgentSpec(model.Spec, result);

        return result;
    }

    /// <inheritdoc/>
    public Task<ApiValidationResult> ValidateToolAsync(ToolDocumentModel model)
    {
        _logger.LogDebug("Validating ToolDocumentModel: {ToolName}", model.Name);

        var result = new ApiValidationResult();

        ValidateResourceMetadata(model.Metadata, result);

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<ApiValidationResult> ValidateConnectorAsync(ConnectorDocumentModel model)
    {
        _logger.LogDebug("Validating ConnectorDocumentModel: {ConnectorName}", model.Name);

        var result = new ApiValidationResult();

        ValidateResourceMetadata(model.Metadata, result);

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<ApiValidationResult> ValidatePluginConfigAsync(PlugInConfigDocumentModel model)
    {
        _logger.LogDebug("Validating PlugInConfigDocumentModel: {PluginName}", model.Name);

        var result = new ApiValidationResult();

        ValidateResourceMetadata(model.Metadata, result);

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<ApiValidationResult> ValidateCommonPromptAsync(CommonPromptDocumentModel model)
    {
        _logger.LogDebug("Validating CommonPromptDocumentModel: {PromptName}", model.Name);

        var result = new ApiValidationResult();

        ValidateResourceMetadata(model.Metadata, result);

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<ApiValidationResult> ValidateCommonToolsListAsync(CommonToolsListDocumentModel model)
    {
        _logger.LogDebug("Validating CommonToolsListDocumentModel: {ListName}", model.Name);

        var result = new ApiValidationResult();

        ValidateResourceMetadata(model.Metadata, result);

        return Task.FromResult(result);
    }

    private void ValidateResourceMetadata(ResourceMetadata metadata, ApiValidationResult result)
    {
        // add validations if necessary
    }

    // Ported from src\Agent\Agent.Core\Validation\AgentValidationService.cs
    // Not reusing it directly because it does not validate against the model
    private async Task ValidateAgentSpec(AgentSpec spec, ApiValidationResult result)
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

        // Validate handoff description is not null
        if (spec.HandoffDescription == null)
        {
            result.AddError("Handoff description cannot be null. Please provide a description to enable agent routing from meta agent, or use an empty string to disable handoff.");
        }

        // Validate handoff description length
        if (!string.IsNullOrWhiteSpace(spec.HandoffDescription) && spec.HandoffDescription.Length > 500)
        {
            result.AddError("Handoff description exceeds maximum length of 500 characters.");
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

        // Validate handoffs is not null
        if (spec.Handoffs == null)
        {
            result.AddError("Handoffs cannot be null. Please provide an array of agent names to enable handoff between agents, or use an empty array [].");
        }

        // Validate handoff names
        if (spec.Handoffs?.Count > 0)
        {
            foreach (var handoffAgent in spec.Handoffs)
            {
                if (string.IsNullOrWhiteSpace(handoffAgent))
                {
                    result.AddError("Handoff name cannot be empty.");
                }
                else if (handoffAgent.Any(char.IsWhiteSpace))
                {
                    result.AddError($"Handoff name '{handoffAgent}' must not contain whitespace.");
                }

                if (handoffAgent == "meta_agent")
                {
                    continue;
                }

                var agent = await _repository.GetAgentByNameAsync(handoffAgent);
                if (agent is null)
                {
                    result.AddError($"Handoff agent '{handoffAgent}' does not exist.");
                }
            }

            // Validate common prompts
            if (spec.CommonPrompts?.Count > 0)
            {
                foreach (var promptName in spec.CommonPrompts)
                {
                    if (string.IsNullOrWhiteSpace(promptName))
                    {
                        result.AddError("Common prompt name cannot be empty.");
                    }

                    if (_systemCommonPrompts.Contains(promptName))
                    {
                        continue;
                    }

                    var prompt = await _repository.GetCommonPromptByNameAsync(promptName);
                    if (prompt is null)
                    {
                        result.AddError($"Common prompt '{promptName}' does not exist.");
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
}
