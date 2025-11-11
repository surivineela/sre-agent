// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;

namespace Agent.Core.Interfaces;

public interface IExtendedAgentRepository
{
    Task<AgentDocumentModel> UpsertAgentAsync(AgentDocumentModel agent, string operationId);

    Task<AgentDocumentModel?> GetAgentByNameAsync(string name);

    Task<PaginatedList<AgentDocumentModel>> GetAgentsAsync(int limit = 50, string? search = null);

    Task<bool> DeleteAgentAsync(string name);

    Task<ToolDocumentModel> UpsertToolAsync(ToolDocumentModel tool, string operationId);

    Task<CommonPromptDocumentModel> UpsertCommonPromptAsync(CommonPromptDocumentModel prompt, string operationId);

    Task<CommonToolsListDocumentModel> UpsertCommonToolsListAsync(CommonToolsListDocumentModel toolsList, string operationId);

    Task<ToolDocumentModel?> GetToolByNameAsync(string name);

    Task<PaginatedList<ToolDocumentModel>> GetToolsAsync(int limit = 50, string? search = null);

    Task<bool> DeleteToolAsync(string name);

    Task<PlugInConfigDocumentModel> UpsertPluginConfigAsync(PlugInConfigDocumentModel config);

    Task<ConnectorDocumentModel> UpsertConnectorAsync(ConnectorDocumentModel connector, string operationId);

    Task<ConnectorDocumentModel?> GetConnectorByNameAsync(string name);

    Task<CommonPromptDocumentModel?> GetCommonPromptByNameAsync(string name);
    Task<PaginatedList<CommonPromptDocumentModel>> GetCommonPromptsAsync(int limit = 50, string? search = null);

    Task<PaginatedList<CommonToolsListDocumentModel>> GetCommonToolsListsAsync(int limit = 50, string? search = null);

    Task<PaginatedList<ConnectorDocumentModel>> GetConnectorsAsync(int limit = 50, string? search = null);

    Task<bool> DeleteConnectorAsync(string name);

    Task<SkillDocumentModel> UpsertSkillDocumentAsync(SkillDocumentModel skill, string operationId);

    Task<bool> DeleteSkillAsync(string name);

    Task<SkillDocumentModel?> GetSkillByNameAsync(string name);

    Task<PaginatedList<SkillDocumentModel>> GetSkillsAsync(int limit = 50, string? search = null, int pageIndex = 1);
}
