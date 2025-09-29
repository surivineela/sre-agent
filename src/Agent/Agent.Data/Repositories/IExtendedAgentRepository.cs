// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;

namespace Agent.Core.Interfaces;

public interface IExtendedAgentRepository
{
    Task<AgentDocumentModel> CreateAgentAsync(AgentDocumentModel agent, string operationId);

    Task<PaginatedList<PlugInConfigDocumentModel>> GetPlugInConfigsAsync(int limit = 50, string? search = null);

    Task<AgentDocumentModel> UpdateAgentAsync(AgentDocumentModel agent, string operationId);

    Task<AgentDocumentModel?> GetAgentByNameAsync(string name);

    Task<PaginatedList<AgentDocumentModel>> GetAgentsAsync(int limit = 50, string? search = null);

    Task<bool> DeleteAgentAsync(string name);

    Task<ToolDocumentModel> CreateToolAsync(ToolDocumentModel tool, string operationId);

    Task<ToolDocumentModel> UpdateToolAsync(ToolDocumentModel tool, string operationId);

    Task<CommonPromptDocumentModel> UpdateCommonPromptAsync(CommonPromptDocumentModel prompt, string operationId);

    Task<CommonToolsListDocumentModel> UpdateCommonToolsListAsync(CommonToolsListDocumentModel toolsList, string operationId);

    Task<ToolDocumentModel?> GetToolByNameAsync(string name);

    Task<PaginatedList<ToolDocumentModel>> GetToolsAsync(int limit = 50, string? search = null);

    Task<bool> DeleteToolAsync(string name);

    Task<PlugInConfigDocumentModel> UpdatePluginConfigAsync(PlugInConfigDocumentModel config);

    Task<ConnectorDocumentModel> CreateConnectorAsync(ConnectorDocumentModel connector, string operationId);

    Task<ConnectorDocumentModel> UpdateConnectorAsync(ConnectorDocumentModel connector, string operationId);

    Task<ConnectorDocumentModel?> GetConnectorByNameAsync(string name);

    Task<PaginatedList<CommonPromptDocumentModel>> GetCommonPromptsAsync(int limit = 50, string? search = null);

    Task<PaginatedList<CommonToolsListDocumentModel>> GetCommonToolsListsAsync(int limit = 50, string? search = null);

    Task<PaginatedList<ConnectorDocumentModel>> GetConnectorsAsync(int limit = 50, string? search = null);

    Task<bool> DeleteConnectorAsync(string name);
}
