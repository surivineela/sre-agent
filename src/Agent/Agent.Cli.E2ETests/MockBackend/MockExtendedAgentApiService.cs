// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Data.DataModels;
using Agent.Web.ApiResources;
using Agent.Web.Models.ExtendedAgents;
using Agent.Web.Models.ExtendedAgents.Request;
using Agent.Web.Models.ExtendedAgents.Response;
using Agent.Web.Services;
using Gremlin.Net.Process.Traversal;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Cli.Tests.E2E.MockBackend;

/// <summary>
/// In-memory implementation of IExtendedAgentApiService for E2E testing.
/// Stores all resources in concurrent dictionaries without any database dependency.
/// </summary>
public class MockExtendedAgentApiService : IExtendedAgentApiService
{
    private readonly ConcurrentDictionary<string, AgentDocumentModel> _agents = new();
    private readonly ConcurrentDictionary<string, ToolDocumentModel> _tools = new();
    private readonly ConcurrentDictionary<string, ConnectorDocumentModel> _connectors = new();
    private readonly ConcurrentDictionary<string, PlugInConfigDocumentModel> _plugins = new();
    private readonly ConcurrentDictionary<string, CommonPromptDocumentModel> _commonPrompts = new();
    private readonly ConcurrentDictionary<string, CommonToolsListDocumentModel> _commonToolLists = new();
    private readonly ConcurrentDictionary<string, SkillDocumentModel> _skills = new();

    // Agent operations
    public Task<ApiCommandResult<AgentDocumentModel>> GetAgentAsync(string agentName)
    {
        if (_agents.TryGetValue(agentName, out var agent))
        {
            return Task.FromResult(new ApiCommandResult<AgentDocumentModel>(agent));
        }
        return Task.FromResult(new ApiCommandResult<AgentDocumentModel>(new NotFoundResult()));
    }

    public Task<ApiCommandResult<AgentDocumentModel>> CreateOrUpdateAgentAsync(string agentName, AgentDocumentModel model, bool dryRun = false)
    {
        if (dryRun)
        {
            return Task.FromResult(new ApiCommandResult<AgentDocumentModel>(model, Guid.NewGuid().ToString()));
        }

        var isUpdate = _agents.ContainsKey(agentName);
        _agents[agentName] = model;

        return Task.FromResult(new ApiCommandResult<AgentDocumentModel>(model, Guid.NewGuid().ToString()));
    }

    public Task<ApiCommandResult<AgentDocumentModel>> DeleteAgentAsync(string agentName, bool dryRun = false)
    {
        if (dryRun)
        {
            return Task.FromResult(new ApiCommandResult<AgentDocumentModel>(new NoContentResult()));
        }

        if (_agents.TryRemove(agentName, out var agent))
        {
            return Task.FromResult(new ApiCommandResult<AgentDocumentModel>(agent, Guid.NewGuid().ToString()));
        }

        return Task.FromResult(new ApiCommandResult<AgentDocumentModel>(new NotFoundResult()));
    }

    public Task<ApiCommandResult<AgentDocumentModel[]>> GetAgentsAsync()
    {
        return Task.FromResult(new ApiCommandResult<AgentDocumentModel[]>([.. _agents.Values]));
    }

    // Tool operations
    public Task<ApiCommandResult<ToolDocumentModel>> GetToolAsync(string toolName)
    {
        if (_tools.TryGetValue(toolName, out var tool))
        {
            return Task.FromResult(new ApiCommandResult<ToolDocumentModel>(tool));
        }
        return Task.FromResult(new ApiCommandResult<ToolDocumentModel>(new NotFoundResult()));
    }

    public Task<ApiCommandResult<ToolDocumentModel>> CreateOrUpdateToolAsync(string toolName, ToolDocumentModel model, bool dryRun = false)
    {
        if (dryRun)
        {
            return Task.FromResult(new ApiCommandResult<ToolDocumentModel>(model, Guid.NewGuid().ToString()));
        }

        _tools[toolName] = model;
        return Task.FromResult(new ApiCommandResult<ToolDocumentModel>(model, Guid.NewGuid().ToString()));
    }

    public Task<ApiCommandResult<ToolDocumentModel>> DeleteToolAsync(string toolName, bool dryRun = false)
    {
        if (dryRun)
        {
            return Task.FromResult(new ApiCommandResult<ToolDocumentModel>(new NoContentResult()));
        }

        if (_tools.TryRemove(toolName, out var tool))
        {
            return Task.FromResult(new ApiCommandResult<ToolDocumentModel>(tool, Guid.NewGuid().ToString()));
        }

        return Task.FromResult(new ApiCommandResult<ToolDocumentModel>(new NotFoundResult()));
    }

    public Task<ApiCommandResult<ToolDocumentModel[]>> GetToolsAsync()
    {
        return Task.FromResult(new ApiCommandResult<ToolDocumentModel[]>([.. _tools.Values]));
    }

    // Connector operations
    public Task<ApiCommandResult<ConnectorDocumentModel>> GetConnectorAsync(string connectorName)
    {
        if (_connectors.TryGetValue(connectorName, out var connector))
        {
            return Task.FromResult(new ApiCommandResult<ConnectorDocumentModel>(connector));
        }
        return Task.FromResult(new ApiCommandResult<ConnectorDocumentModel>(new NotFoundResult()));
    }

    public Task<ApiCommandResult<ConnectorDocumentModel>> CreateOrUpdateConnectorAsync(string connectorName, ConnectorDocumentModel model, bool dryRun = false)
    {
        if (dryRun)
        {
            return Task.FromResult(new ApiCommandResult<ConnectorDocumentModel>(model, Guid.NewGuid().ToString()));
        }

        _connectors[connectorName] = model;
        return Task.FromResult(new ApiCommandResult<ConnectorDocumentModel>(model, Guid.NewGuid().ToString()));
    }

    public Task<ApiCommandResult<ConnectorDocumentModel>> DeleteConnectorAsync(string connectorName, bool dryRun = false)
    {
        if (dryRun)
        {
            return Task.FromResult(new ApiCommandResult<ConnectorDocumentModel>(new NoContentResult()));
        }

        if (_connectors.TryRemove(connectorName, out var connector))
        {
            return Task.FromResult(new ApiCommandResult<ConnectorDocumentModel>(connector, Guid.NewGuid().ToString()));
        }

        return Task.FromResult(new ApiCommandResult<ConnectorDocumentModel>(new NotFoundResult()));
    }

    public Task<ApiCommandResult<ConnectorDocumentModel[]>> GetConnectorsAsync()
    {
        return Task.FromResult(new ApiCommandResult<ConnectorDocumentModel[]>([.. _connectors.Values]));
    }

    public Task<ApiCommandResult<ConnectorStatusResponse>> GetConnectorStatusAsync(string connectorName)
    {
        if (_connectors.ContainsKey(connectorName))
        {
            var status = new ConnectorStatusResponse
            {
                Status = "Connected",
                Message = "Mock connector is always connected"
            };
            return Task.FromResult(new ApiCommandResult<ConnectorStatusResponse>(status));
        }
        return Task.FromResult(new ApiCommandResult<ConnectorStatusResponse>(new NotFoundResult()));
    }

    // Plugin operations
    public Task<ApiCommandResult<PlugInConfigDocumentModel>> GetPluginConfigAsync(string pluginName)
    {
        if (_plugins.TryGetValue(pluginName, out var plugin))
        {
            return Task.FromResult(new ApiCommandResult<PlugInConfigDocumentModel>(plugin));
        }
        return Task.FromResult(new ApiCommandResult<PlugInConfigDocumentModel>(new NotFoundResult()));
    }

    public Task<ApiCommandResult<PlugInConfigDocumentModel>> CreateOrUpdatePluginConfigAsync(string pluginName, PlugInConfigDocumentModel model, bool dryRun = false)
    {
        if (dryRun)
        {
            return Task.FromResult(new ApiCommandResult<PlugInConfigDocumentModel>(model, Guid.NewGuid().ToString()));
        }

        _plugins[pluginName] = model;
        return Task.FromResult(new ApiCommandResult<PlugInConfigDocumentModel>(model, Guid.NewGuid().ToString()));
    }

    public Task<ApiCommandResult<PlugInConfigDocumentModel>> DeletePluginConfigAsync(string pluginName, bool dryRun = false)
    {
        if (dryRun)
        {
            return Task.FromResult(new ApiCommandResult<PlugInConfigDocumentModel>(new NoContentResult()));
        }

        if (_plugins.TryRemove(pluginName, out var plugin))
        {
            return Task.FromResult(new ApiCommandResult<PlugInConfigDocumentModel>(plugin, Guid.NewGuid().ToString()));
        }

        return Task.FromResult(new ApiCommandResult<PlugInConfigDocumentModel>(new NotFoundResult()));
    }

    public Task<ApiCommandResult<PlugInConfigDocumentModel[]>> GetPluginConfigsAsync()
    {
        return Task.FromResult(new ApiCommandResult<PlugInConfigDocumentModel[]>([.. _plugins.Values]));
    }

    // CommonPrompt operations
    public Task<ApiCommandResult<CommonPromptDocumentModel>> GetCommonPromptAsync(string promptName)
    {
        if (_commonPrompts.TryGetValue(promptName, out var prompt))
        {
            return Task.FromResult(new ApiCommandResult<CommonPromptDocumentModel>(prompt));
        }
        return Task.FromResult(new ApiCommandResult<CommonPromptDocumentModel>(new NotFoundResult()));
    }

    public Task<ApiCommandResult<CommonPromptDocumentModel>> CreateOrUpdateCommonPromptAsync(string promptName, CommonPromptDocumentModel model, bool dryRun = false)
    {
        if (dryRun)
        {
            return Task.FromResult(new ApiCommandResult<CommonPromptDocumentModel>(model, Guid.NewGuid().ToString()));
        }

        _commonPrompts[promptName] = model;
        return Task.FromResult(new ApiCommandResult<CommonPromptDocumentModel>(model, Guid.NewGuid().ToString()));
    }

    public Task<ApiCommandResult<CommonPromptDocumentModel>> DeleteCommonPromptAsync(string promptName, bool dryRun = false)
    {
        if (dryRun)
        {
            return Task.FromResult(new ApiCommandResult<CommonPromptDocumentModel>(new NoContentResult()));
        }

        if (_commonPrompts.TryRemove(promptName, out var prompt))
        {
            return Task.FromResult(new ApiCommandResult<CommonPromptDocumentModel>(prompt, Guid.NewGuid().ToString()));
        }

        return Task.FromResult(new ApiCommandResult<CommonPromptDocumentModel>(new NotFoundResult()));
    }

    public Task<ApiCommandResult<CommonPromptDocumentModel[]>> GetCommonPromptsAsync()
    {
        return Task.FromResult(new ApiCommandResult<CommonPromptDocumentModel[]>([.. _commonPrompts.Values]));
    }

    // CommonToolList operations
    public Task<ApiCommandResult<CommonToolsListDocumentModel>> GetCommonToolListAsync(string listName)
    {
        if (_commonToolLists.TryGetValue(listName, out var list))
        {
            return Task.FromResult(new ApiCommandResult<CommonToolsListDocumentModel>(list));
        }
        return Task.FromResult(new ApiCommandResult<CommonToolsListDocumentModel>(new NotFoundResult()));
    }

    public Task<ApiCommandResult<CommonToolsListDocumentModel>> CreateOrUpdateCommonToolListAsync(string listName, CommonToolsListDocumentModel model, bool dryRun = false)
    {
        if (dryRun)
        {
            return Task.FromResult(new ApiCommandResult<CommonToolsListDocumentModel>(model, Guid.NewGuid().ToString()));
        }

        _commonToolLists[listName] = model;
        return Task.FromResult(new ApiCommandResult<CommonToolsListDocumentModel>(model, Guid.NewGuid().ToString()));
    }

    public Task<ApiCommandResult<CommonToolsListDocumentModel>> DeleteCommonToolListAsync(string listName, bool dryRun = false)
    {
        if (dryRun)
        {
            return Task.FromResult(new ApiCommandResult<CommonToolsListDocumentModel>(new NoContentResult()));
        }

        if (_commonToolLists.TryRemove(listName, out var list))
        {
            return Task.FromResult(new ApiCommandResult<CommonToolsListDocumentModel>(list, Guid.NewGuid().ToString()));
        }

        return Task.FromResult(new ApiCommandResult<CommonToolsListDocumentModel>(new NotFoundResult()));
    }

    public Task<ApiCommandResult<CommonToolsListDocumentModel[]>> GetCommonToolListsAsync()
    {
        return Task.FromResult(new ApiCommandResult<CommonToolsListDocumentModel[]>([.. _commonToolLists.Values]));
    }

    // Skill operations
    public Task<ApiCommandResult<SkillDocumentModel>> GetSkillAsync(string skillName)
    {
        if (_skills.TryGetValue(skillName, out var skill))
        {
            return Task.FromResult(new ApiCommandResult<SkillDocumentModel>(skill));
        }
        return Task.FromResult(new ApiCommandResult<SkillDocumentModel>(new NotFoundResult()));
    }

    public Task<ApiCommandResult<SkillDocumentModel>> CreateOrUpdateSkillAsync(string skillName, SkillDocumentModel model)
    {
        _skills[skillName] = model;
        return Task.FromResult(new ApiCommandResult<SkillDocumentModel>(model, Guid.NewGuid().ToString()));
    }

    public Task<ApiCommandResult<SkillDocumentModel>> DeleteSkillAsync(string skillName)
    {
        if (_skills.TryRemove(skillName, out var skill))
        {
            return Task.FromResult(new ApiCommandResult<SkillDocumentModel>(skill, Guid.NewGuid().ToString()));
        }

        return Task.FromResult(new ApiCommandResult<SkillDocumentModel>(new NotFoundResult()));
    }

    public Task<ApiCommandResult<SkillDocumentModel[]>> GetSkillsAsync()
    {
        return Task.FromResult(new ApiCommandResult<SkillDocumentModel[]>([.. _skills.Values]));
    }

    public Task<ApiCommandResult<SkillDocumentModel>> ConvertAgentToSkillAsync(string agentName, List<string> topLevelAgents)
    {
        // Simple mock: copy agent to skill if it exists
        if (_agents.TryGetValue(agentName, out var agent))
        {
            var skill = new SkillDocumentModel(
                Metadata: agent.Metadata,
                Spec: new Agent.Framework.Skills.SkillSpec
                {
                    Name = agent.Name,
                    Description = agent.Spec.HandoffDescription ?? string.Empty,
                    Tools = agent.Spec.Tools ?? new List<string>(),
                    SkillMdContent = agent.Spec.Instructions ?? string.Empty,
                    AdditionalFiles = new List<Agent.Framework.Skills.SkillSubFile>()
                }
            );

            _skills[agentName] = skill;
            return Task.FromResult(new ApiCommandResult<SkillDocumentModel>(skill));
        }

        return Task.FromResult(new ApiCommandResult<SkillDocumentModel>(new NotFoundResult()));
    }

    public Task<KustoQueryTestResponse> TestKustoQueryAsync(string toolName, KustoQueryTestRequest request, CancellationToken cancellationToken)
    {
        // Simple mock response
        var response = new KustoQueryTestResponse
        {
            Success = true,
            RowCount = 1,
            Columns = new List<string> { "Column1", "Column2" },
            Rows = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    { "Column1", "Value1" },
                    { "Column2", "Value2" }
                }
            },
            ExecutionTimeMs = 100,
            QueryExecuted = "mock query"
        };

        return Task.FromResult(response);
    }

    /// <summary>
    /// Clear all data - useful for test cleanup
    /// </summary>
    public void Clear()
    {
        _agents.Clear();
        _tools.Clear();
        _connectors.Clear();
        _plugins.Clear();
        _commonPrompts.Clear();
        _commonToolLists.Clear();
        _skills.Clear();
    }

    // Helper methods for test assertions

    /// <summary>
    /// Get all tool names (for test assertions)
    /// </summary>
    public IEnumerable<string> GetToolNames() => _tools.Keys;

    /// <summary>
    /// Get a specific tool by name (for test assertions)
    /// </summary>
    public ToolDocumentModel? GetTool(string toolName)
    {
        _tools.TryGetValue(toolName, out var tool);
        return tool;
    }

    /// <summary>
    /// Get all agent names (for test assertions)
    /// </summary>
    public IEnumerable<string> GetAgentNames() => _agents.Keys;

    /// <summary>
    /// Get a specific agent by name (for test assertions)
    /// </summary>
    public AgentDocumentModel? GetAgent(string agentName)
    {
        _agents.TryGetValue(agentName, out var agent);
        return agent;
    }
}
