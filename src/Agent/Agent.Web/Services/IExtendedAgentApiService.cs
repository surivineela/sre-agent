// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Web.ApiResources;

namespace Agent.Web.Services;

public interface IExtendedAgentApiService
{
    // Agent operations
    Task<ApiCommandResult<AgentDocumentModel>> GetAgentAsync(string agentName);
    Task<ApiCommandResult<AgentDocumentModel>> CreateOrUpdateAgentAsync(string agentName, AgentDocumentModel model, bool dryRun = false);
    Task<ApiCommandResult<AgentDocumentModel>> DeleteAgentAsync(string agentName, bool dryRun = false);
    Task<ApiCommandResult<AgentDocumentModel[]>> GetAgentsAsync(int limit = 50, string? search = null);

    // Tool operations
    Task<ApiCommandResult<ToolDocumentModel>> GetToolAsync(string toolName);
    Task<ApiCommandResult<ToolDocumentModel>> CreateOrUpdateToolAsync(string toolName, ToolDocumentModel model, bool dryRun = false);
    Task<ApiCommandResult<ToolDocumentModel>> DeleteToolAsync(string toolName, bool dryRun = false);
    Task<ApiCommandResult<ToolDocumentModel[]>> GetToolsAsync(int limit = 50, string? search = null);

    // Connector operations
    Task<ApiCommandResult<ConnectorDocumentModel>> GetConnectorAsync(string connectorName);
    Task<ApiCommandResult<ConnectorDocumentModel>> CreateOrUpdateConnectorAsync(string connectorName, ConnectorDocumentModel model, bool dryRun = false);
    Task<ApiCommandResult<ConnectorDocumentModel>> DeleteConnectorAsync(string connectorName, bool dryRun = false);
    Task<ApiCommandResult<ConnectorDocumentModel[]>> GetConnectorsAsync(int limit = 50, string? search = null);

    // Plugin operations
    Task<ApiCommandResult<PlugInConfigDocumentModel>> GetPluginConfigAsync(string pluginName);
    Task<ApiCommandResult<PlugInConfigDocumentModel>> CreateOrUpdatePluginConfigAsync(string pluginName, PlugInConfigDocumentModel model, bool dryRun = false);
    Task<ApiCommandResult<PlugInConfigDocumentModel>> DeletePluginConfigAsync(string pluginName, bool dryRun = false);
    Task<ApiCommandResult<PlugInConfigDocumentModel[]>> GetPluginConfigsAsync(int limit = 50, string? search = null);

    // CommonPrompt operations
    Task<ApiCommandResult<CommonPromptDocumentModel>> GetCommonPromptAsync(string promptName);
    Task<ApiCommandResult<CommonPromptDocumentModel>> CreateOrUpdateCommonPromptAsync(string promptName, CommonPromptDocumentModel model, bool dryRun = false);
    Task<ApiCommandResult<CommonPromptDocumentModel>> DeleteCommonPromptAsync(string promptName, bool dryRun = false);
    Task<ApiCommandResult<CommonPromptDocumentModel[]>> GetCommonPromptsAsync(int limit = 50, string? search = null);

    // CommonToolList operations
    Task<ApiCommandResult<CommonToolsListDocumentModel>> GetCommonToolListAsync(string listName);
    Task<ApiCommandResult<CommonToolsListDocumentModel>> CreateOrUpdateCommonToolListAsync(string listName, CommonToolsListDocumentModel model, bool dryRun = false);
    Task<ApiCommandResult<CommonToolsListDocumentModel>> DeleteCommonToolListAsync(string listName, bool dryRun = false);
    Task<ApiCommandResult<CommonToolsListDocumentModel[]>> GetCommonToolListsAsync(int limit = 50, string? search = null);

    // Skill operations
    Task<ApiCommandResult<SkillDocumentModel>> GetSkillAsync(string skillName);
    Task<ApiCommandResult<SkillDocumentModel>> CreateOrUpdateSkillAsync(string skillName, SkillDocumentModel model);
    Task<ApiCommandResult<SkillDocumentModel>> DeleteSkillAsync(string skillName);
    Task<ApiCommandResult<SkillDocumentModel[]>> GetSkillsAsync(int limit = 50, string? search = null);
    Task<ApiCommandResult<SkillDocumentModel>> ConvertAgentToSkillAsync(string agentName, List<string> topLevelAgents);
}
