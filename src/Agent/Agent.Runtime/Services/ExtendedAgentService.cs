// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data;
using Agent.Framework;
using Agent.Runtime.Interfaces;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

public class ExtendedAgentService : IExtendedAgentService
{
    private readonly ILogger<ExtendedAgentService> _logger;
    private readonly IAgentFactory<AgentContext> _agentFactory;
    private readonly IToolFactory<AgentContext> _toolFactory;
    private readonly IExtendedAgentRepository _extendedAgentRepository;

    public ExtendedAgentService(
        ILogger<ExtendedAgentService> logger,
        IAgentFactory<AgentContext> agentFactory,
        IToolFactory<AgentContext> toolFactory,
        IExtendedAgentRepository extendedAgentRepository

        )
    {
        _logger = logger;
        _agentFactory = agentFactory;
        _toolFactory = toolFactory;
        _extendedAgentRepository = extendedAgentRepository;
    }

    public async Task<PaginatedList<YamlAgentDescriptor>> GetAgentsAsync(int pageIndex, int limit, string? search)
    {
        var all = await _extendedAgentRepository.GetAgentsAsync();

        var filtered = all
            .Where(a => string.IsNullOrEmpty(search) || a.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

        var mapped = filtered
            .Select(c => DocumentToRuntimeMapper.ToRuntimeAgent(c));

        return PaginatedList<YamlAgentDescriptor>.Create(mapped, pageIndex, limit);
    }

    public async Task<PaginatedList<YamlToolDefinitionBase>> GetToolsAsync(int pageIndex, int limit, string? search)
    {
        try
        {
            var all = await _extendedAgentRepository.GetToolsAsync(); // remove limit from repository call
            var filtered = all
                .Where(t => string.IsNullOrEmpty(search) || t.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            var mapped = filtered
                .Select(t => DocumentToRuntimeMapper.ToRuntimeTool(t));

            return PaginatedList<YamlToolDefinitionBase>.Create(mapped, pageIndex, limit);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to retrieve tools with search '{Search}' and limit {Limit}", search ?? "none", limit);
            throw;
        }
    }

    public async Task RefreshAgentAndToolsRegisterationsAsync()
    {
        _logger.LogInternalInformation("Starting custom agent files download...");

        try
        {
            _logger.LogInternalInformation("Loading extended tools from Cosmos DB on demand...");

            // Load all extended tools
            var extendedTools = await _extendedAgentRepository.GetToolsAsync(limit: 1000);

            // Load new ones
            foreach (var extendedTool in extendedTools)
            {
                try
                {
                    var concretetool = DocumentToRuntimeMapper.ToRuntimeTool(extendedTool);

                    _toolFactory.RegisterTool(concretetool, BehaviorOnNameConflict.Overwrite);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to load extended tool {ToolName} from Cosmos DB on demand", extendedTool.Name);
                    // Continue loading other tools even if one fails
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to load extended tools from Cosmos DB on demand");
            throw;
        }

        try
        {
            var commonToolsLists = await _extendedAgentRepository.GetCommonToolsListsAsync(limit: 1000);
            foreach (var toolsList in commonToolsLists)
            {
                try
                {
                    var concreteToolsList = DocumentToRuntimeMapper.ToRuntimeToolsList(toolsList);
                    _agentFactory.LoadCommonToolsListFromDescriptor(concreteToolsList);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to load common tools list from Cosmos: {ToolsListName}", toolsList.Name);
                }
            }

            var commonPrompts = await _extendedAgentRepository.GetCommonPromptsAsync(limit: 1000);
            foreach (var prompt in commonPrompts)
            {
                try
                {
                    var concretePrompt = DocumentToRuntimeMapper.ToRuntimePrompt(prompt);
                    _agentFactory.LoadCommonPromptFromDescriptor(concretePrompt);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to load common prompt from Cosmos: {PromptName}", prompt.Name);
                }
            }

            var extendedAgents = await _extendedAgentRepository.GetAgentsAsync(limit: 1000);
            var loadedAgentNames = new List<string>();
            foreach (var extendedAgent in extendedAgents)
            {
                try
                {
                    var concreteAgent = DocumentToRuntimeMapper.ToRuntimeAgent(extendedAgent);
                    _agentFactory.LoadAgentFromDescriptor(concreteAgent, true);
                    _logger.LogInternalInformation("Loaded extended agent from Cosmos: {AgentName}", extendedAgent.Name);
                    loadedAgentNames.Add(extendedAgent.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to load extended agent from Cosmos: {AgentName}", extendedAgent.Name);
                }
            }
            _agentFactory.UpdateHandoffs();
            _logger.LogInternalInformation("Completed loading extended agents from Cosmos: {AgentNames}", string.Join(", ", loadedAgentNames));
            // load tools stored in Cosmos
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "failed to load extended agents and tools");
            return;
        }
    }

    public async Task<bool> DeleteAgentAsync(string agentName)
    {
        try
        {
            _logger.LogInternalInformation("Deleting agent {AgentName}", agentName);

            var deleted = await _extendedAgentRepository.DeleteAgentAsync(agentName);

            if (deleted)
            {
                _logger.LogInternalInformation("Successfully deleted agent {AgentName} from repository", agentName);

                // Note: AgentFactory doesn't have a deregister method, so the agent will remain
                // in memory until the service is restarted or agents are refreshed
                _logger.LogInternalWarning("Agent {AgentName} removed from storage but may remain in memory until service restart", agentName);
            }
            else
            {
                _logger.LogInternalWarning("Agent {AgentName} not found for deletion", agentName);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to delete agent {AgentName}", agentName);
            throw;
        }
    }

    public async Task<(bool deleted, List<string> dependentAgents)> DeleteToolAsync(string toolName)
    {
        try
        {
            _logger.LogInternalInformation("Deleting tool {ToolName}", toolName);

            // Check if any agents depend on this tool
            var allAgents = await _extendedAgentRepository.GetAgentsAsync(limit: 1000);
            var dependentAgents = allAgents
                .Where(agent => agent.Tools.Contains(toolName))
                .Select(agent => agent.Name)
                .ToList();

            if (dependentAgents.Any())
            {
                _logger.LogInternalWarning("Cannot delete tool {ToolName} because it is used by agents: {DependentAgents}",
                    toolName, string.Join(", ", dependentAgents));
                return (false, dependentAgents);
            }

            var deleted = await _extendedAgentRepository.DeleteToolAsync(toolName);

            if (deleted)
            {
                _logger.LogInternalInformation("Successfully deleted tool {ToolName} from repository", toolName);

                // Note: ToolFactory doesn't have a deregister method, so the tool will remain
                // in memory until the service is restarted or tools are refreshed
                _logger.LogInternalWarning("Tool {ToolName} removed from storage but may remain in memory until service restart", toolName);
            }
            else
            {
                _logger.LogInternalWarning("Tool {ToolName} not found for deletion", toolName);
            }

            return (deleted, []);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to delete tool {ToolName}", toolName);
            throw;
        }
    }

    public async Task LoadExtendedCommonToolsListsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Starting custom agent files download...");

        try
        {
            var commonToolsLists = await _extendedAgentRepository.GetCommonToolsListsAsync(limit: 1000);
            foreach (var toolsList in commonToolsLists)
            {
                try
                {
                    var concreteToolsList = DocumentToRuntimeMapper.ToRuntimeToolsList(toolsList);
                    _agentFactory.LoadCommonToolsListFromDescriptor(concreteToolsList);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to load common tools list from Cosmos: {ToolsListName}", toolsList.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "failed to load extended agents and tools");
            return;
        }
    }

    public async Task LoadExtendedCommonPromptsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var commonPrompts = await _extendedAgentRepository.GetCommonPromptsAsync(limit: 1000);
            foreach (var prompt in commonPrompts)
            {
                try
                {
                    var concretePrompt = DocumentToRuntimeMapper.ToRuntimePrompt(prompt);
                    _agentFactory.LoadCommonPromptFromDescriptor(concretePrompt);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to load common prompt from Cosmos: {PromptName}", prompt.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "failed to load extended agents and tools");
            return;
        }
    }



    public async Task LoadExtendedAgentsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Starting custom agent files download...");

        try
        {
            var extendedAgents = await _extendedAgentRepository.GetAgentsAsync(limit: 1000);
            var loadedAgentNames = new List<string>();
            foreach (var extendedAgent in extendedAgents)
            {
                try
                {
                    var concreteAgent = DocumentToRuntimeMapper.ToRuntimeAgent(extendedAgent);
                    _agentFactory.LoadAgentFromDescriptor(concreteAgent, true);
                    _logger.LogInternalInformation("Loaded extended agent from Cosmos: {AgentName}", extendedAgent.Name);
                    loadedAgentNames.Add(extendedAgent.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to load extended agent from Cosmos: {AgentName}", extendedAgent.Name);
                }
            }

            // load tools stored in Cosmos
            _logger.LogInternalInformation("Completed loading extended agents from Cosmos: {AgentNames}", string.Join(", ", loadedAgentNames));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "failed to load extended agents and tools");
            return;
        }
    }

    /// <summary>
    /// Validates YAML structure to catch common indentation mistakes
    /// </summary>
    public List<string> ValidateYamlStructure(Dictionary<string, object> rootDocument)
    {
        var errors = new List<string>();

        // Extract spec section
        if (!rootDocument.TryGetValue("spec", out var specObj))
        {
            // If no spec section, return early - this will be caught by other validation
            return errors;
        }

        Dictionary<string, object> spec;
        if (specObj is Dictionary<string, object> stringSpec)
        {
            spec = stringSpec;
        }
        else if (specObj is Dictionary<object, object> objectSpec)
        {
            spec = objectSpec.ToDictionary(
                kvp => kvp.Key.ToString()!,
                kvp => kvp.Value
            );
        }
        else
        {
            return errors;
        }

        // Define agent properties that should be under 'spec', not at root level
        var agentProperties = new HashSet<string>
        {
            "name", "system_prompt", "tools", "handoffs", "mcp_tools",
            "temperature", "max_reflection_count", "handoff_description", "common_prompts",
            "common_tools", "allow_parallel_tool_calls", "agents_as_tools", "custom_reflection_note",
            "critic_prompt_path", "critic_on_handoff", "disable_document_retrieval",
            "instructions_override", "enable_handoff_prompt_override", "handoff_prompt_override",
            "user_prompt_override", "llm_model_name", "disable_common_prompts", "agent_type",
            "parameter_extraction_agent", "orchestration_start_agents", "result_summarization_prompt",
            "next_agent_mappings", "output_type"
        };

        // Check for agent properties at root level (common indentation mistake)
        foreach (var property in agentProperties)
        {
            if (rootDocument.ContainsKey(property))
            {
                errors.Add($"Property '{property}' should be under 'spec' section, not at root level. Check indentation.");
            }
        }

        // Check if spec section is missing required properties
        if (!spec.ContainsKey("name"))
        {
            errors.Add("Required property 'name' is missing from 'spec' section");
        }

        // Check for required system_prompt property
        if (!spec.ContainsKey("system_prompt"))
        {
            // Check if it's at root level due to indentation error
            if (rootDocument.ContainsKey("system_prompt"))
            {
                errors.Add("Property 'system_prompt' found at root level - should be under 'spec' section. Check indentation.");
            }
            else
            {
                errors.Add("Required property 'system_prompt' is missing from 'spec' section");
            }
        }

        // Check for invalid 'instructions' property usage
        if (spec.ContainsKey("instructions"))
        {
            errors.Add("Use 'system_prompt' instead of 'instructions' in the 'spec' section");
        }

        // Validate that spec has some content
        if (spec.Count == 0)
        {
            errors.Add("'spec' section is empty - agent properties should be defined here");
        }

        return errors;
    }
}
