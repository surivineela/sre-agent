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

    Task<IReadOnlyList<AgentDocumentModel>> GetAllAgentsAsync();

    Task<bool> DeleteAgentAsync(string name);

    Task<ToolDocumentModel> UpsertToolAsync(ToolDocumentModel tool, string operationId);

    Task<CommonPromptDocumentModel> UpsertCommonPromptAsync(CommonPromptDocumentModel prompt, string operationId);

    Task<CommonToolsListDocumentModel> UpsertCommonToolsListAsync(CommonToolsListDocumentModel toolsList, string operationId);

    Task<ToolDocumentModel?> GetToolByNameAsync(string name);

    Task<PaginatedList<ToolDocumentModel>> GetToolsAsync(int limit = 50, string? search = null);

    Task<IReadOnlyList<ToolDocumentModel>> GetAllToolsAsync();

    Task<bool> DeleteToolAsync(string name);

    Task<PlugInConfigDocumentModel> UpsertPluginConfigAsync(PlugInConfigDocumentModel config);

    Task<PlugInConfigDocumentModel?> GetPluginConfigByNameAsync(string name);

    Task<PaginatedList<PlugInConfigDocumentModel>> GetPluginConfigsAsync(int limit = 50, string? search = null);

    Task<IReadOnlyList<PlugInConfigDocumentModel>> GetAllPluginConfigsAsync();

    Task<bool> DeletePluginConfigAsync(string name);

    Task<ConnectorDocumentModel> UpsertConnectorAsync(ConnectorDocumentModel connector, string operationId);

    Task<ConnectorDocumentModel?> GetConnectorByNameAsync(string name);

    Task<CommonPromptDocumentModel?> GetCommonPromptByNameAsync(string name);
    Task<PaginatedList<CommonPromptDocumentModel>> GetCommonPromptsAsync(int limit = 50, string? search = null);
    Task<IReadOnlyList<CommonPromptDocumentModel>> GetAllCommonPromptsAsync();
    Task<bool> DeleteCommonPromptAsync(string name);

    Task<CommonToolsListDocumentModel?> GetCommonToolsListByNameAsync(string name);
    Task<PaginatedList<CommonToolsListDocumentModel>> GetCommonToolsListsAsync(int limit = 50, string? search = null);
    Task<IReadOnlyList<CommonToolsListDocumentModel>> GetAllCommonToolsListsAsync();
    Task<bool> DeleteCommonToolsListAsync(string name);

    Task<PaginatedList<ConnectorDocumentModel>> GetConnectorsAsync(int limit = 50, string? search = null);

    Task<IReadOnlyList<ConnectorDocumentModel>> GetAllConnectorsAsync();

    Task<bool> DeleteConnectorAsync(string name);

    Task<SkillDocumentModel> UpsertSkillDocumentAsync(SkillDocumentModel skill, string operationId);

    Task<bool> DeleteSkillAsync(string name);

    Task<SkillDocumentModel?> GetSkillByNameAsync(string name);

    Task<PaginatedList<SkillDocumentModel>> GetSkillsAsync(int limit = 50, string? search = null, int pageIndex = 1);

    Task<IReadOnlyList<SkillDocumentModel>> GetAllSkillsAsync();

    #region Migration Operations
    /// <summary>
    /// Migrates all tool documents to use the new id and partitionKey format.
    /// </summary>
    Task MigrateToolDocumentsAsync();

    /// <summary>
    /// Migrates all agent documents to use the new id and partitionKey format.
    /// </summary>
    Task MigrateAgentDocumentsAsync();

    /// <summary>
    /// Migrates all connector documents to use the new id and partitionKey format.
    /// </summary>
    Task MigrateConnectorDocumentsAsync();

    /// <summary>
    /// Migrates all common prompt documents to use the new id and partitionKey format.
    /// </summary>
    Task MigrateCommonPromptDocumentsAsync();

    /// <summary>
    /// Migrates all common tools list documents to use the new id and partitionKey format.
    /// </summary>
    Task MigrateCommonToolsListDocumentsAsync();

    /// <summary>
    /// Migrates all skill documents to use the new id and partitionKey format.
    /// </summary>
    Task MigrateSkillDocumentsAsync();

    /// <summary>
    /// Migrates all plugin config documents to use the new id and partitionKey format.
    /// </summary>
    Task MigratePluginConfigDocumentsAsync();

    #endregion
}
