using Agent.Data.DataModels;
using Agent.Web.ApiResources;

namespace Agent.Web.Services;

public interface IExtendedAgentApiService
{
    // Agent operations
    Task<ApiCommandResult<AgentDocumentModel>> GetAgentAsync(string agentName);
    Task<ApiCommandResult<AgentDocumentModel>> CreateOrUpdateAgentAsync(string agentName, AgentDocumentModel model);
    Task<ApiCommandResult<AgentDocumentModel>> DeleteAgentAsync(string agentName);
    Task<ApiCommandResult<AgentDocumentModel[]>> GetAgentsAsync(int limit = 50, string? search = null);

    // Tool operations
    Task<ApiCommandResult<ToolDocumentModel>> GetToolAsync(string toolName);
    Task<ApiCommandResult<ToolDocumentModel>> CreateOrUpdateToolAsync(string toolName, ToolDocumentModel model);
    Task<ApiCommandResult<ToolDocumentModel>> DeleteToolAsync(string toolName);
    Task<ApiCommandResult<ToolDocumentModel[]>> GetToolsAsync(int limit = 50, string? search = null);

    // Connector operations
    Task<ApiCommandResult<ConnectorDocumentModel>> GetConnectorAsync(string connectorName);
    Task<ApiCommandResult<ConnectorDocumentModel>> CreateOrUpdateConnectorAsync(string connectorName, ConnectorDocumentModel model);
    Task<ApiCommandResult<ConnectorDocumentModel>> DeleteConnectorAsync(string connectorName);
    Task<ApiCommandResult<ConnectorDocumentModel[]>> GetConnectorsAsync(int limit = 50, string? search = null);

    // Plugin operations
    Task<ApiCommandResult<PlugInConfigDocumentModel>> GetPluginConfigAsync(string pluginName);
    Task<ApiCommandResult<PlugInConfigDocumentModel>> CreateOrUpdatePluginConfigAsync(string pluginName, PlugInConfigDocumentModel model);
    Task<ApiCommandResult<PlugInConfigDocumentModel>> DeletePluginConfigAsync(string pluginName);
    Task<ApiCommandResult<PlugInConfigDocumentModel[]>> GetPluginConfigsAsync(int limit = 50, string? search = null);

    // CommonPrompt operations
    Task<ApiCommandResult<CommonPromptDocumentModel>> GetCommonPromptAsync(string promptName);
    Task<ApiCommandResult<CommonPromptDocumentModel>> CreateOrUpdateCommonPromptAsync(string promptName, CommonPromptDocumentModel model);
    Task<ApiCommandResult<CommonPromptDocumentModel>> DeleteCommonPromptAsync(string promptName);
    Task<ApiCommandResult<CommonPromptDocumentModel[]>> GetCommonPromptsAsync(int limit = 50, string? search = null);

    // CommonToolList operations
    Task<ApiCommandResult<CommonToolsListDocumentModel>> GetCommonToolListAsync(string listName);
    Task<ApiCommandResult<CommonToolsListDocumentModel>> CreateOrUpdateCommonToolListAsync(string listName, CommonToolsListDocumentModel model);
    Task<ApiCommandResult<CommonToolsListDocumentModel>> DeleteCommonToolListAsync(string listName);
    Task<ApiCommandResult<CommonToolsListDocumentModel[]>> GetCommonToolListsAsync(int limit = 50, string? search = null);
}
