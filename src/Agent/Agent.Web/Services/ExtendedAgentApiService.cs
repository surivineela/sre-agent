using Agent.Core.Interfaces;
using Agent.Data.DataModels;
using Agent.Logging;
using Agent.Runtime.Interfaces;
using Agent.Web.ApiResources;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Services;

public class ExtendedAgentApiService : IExtendedAgentApiService
{
    private readonly ILogger<ExtendedAgentApiService> _logger;
    private readonly IExtendedAgentService _extendedAgentService;
    private readonly IExtendedAgentRepository _repository;

    public ExtendedAgentApiService(
        ILogger<ExtendedAgentApiService> logger,
        IExtendedAgentService extendedAgentService,
        IExtendedAgentRepository repository
    )
    {
        _logger = logger;
        _extendedAgentService = extendedAgentService;
        _repository = repository;
    }

    public async Task<ApiCommandResult<AgentDocumentModel>> CreateOrUpdateAgentAsync(string agentName, AgentDocumentModel model)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Creating or updating extended agent: {AgentName}, OperationId: {OperationId}", agentName, operationId);
            var agent = await _repository.UpsertAgentAsync(model, operationId);
            await _extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();

            return new ApiCommandResult<AgentDocumentModel>(agent, operationId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while creating or updating extended agent: {AgentName}", agentName);
            throw;
        }
    }

    public async Task<ApiCommandResult<AgentDocumentModel>> DeleteAgentAsync(string agentName)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Deleting extended agent: {AgentName}, OperationId: {OperationId}", agentName, operationId);

            var agent = await _repository.GetAgentByNameAsync(agentName);
            if (agent == null)
            {
                return new ApiCommandResult<AgentDocumentModel>(new NoContentResult());
            }

            await _repository.DeleteAgentAsync(agentName);

            return new ApiCommandResult<AgentDocumentModel>(agent, operationId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while deleting extended agent: {AgentName}", agentName);
            throw;
        }
    }

    public async Task<ApiCommandResult<AgentDocumentModel>> GetAgentAsync(string agentName)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Retrieving extended agent: {AgentName}, OperationId: {OperationId}", agentName, operationId);

            var agent = await _repository.GetAgentByNameAsync(agentName);
            if (agent == null)
            {
                return new ApiCommandResult<AgentDocumentModel>(new NotFoundResult());
            }

            return new ApiCommandResult<AgentDocumentModel>(agent);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while retrieving extended agent: {AgentName}", agentName);
            throw;
        }
    }

    public async Task<ApiCommandResult<AgentDocumentModel[]>> GetAgentsAsync(int limit = 50, string? search = null)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Getting extended agents, Limit: {Limit}, Search: {Search}, OperationId: {OperationId}", limit, search, operationId);

            var agents = await _repository.GetAgentsAsync(limit, search);

            return new ApiCommandResult<AgentDocumentModel[]>(agents.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while listing extended agents");
            throw;
        }
    }

    // Tool operations
    public async Task<ApiCommandResult<ToolDocumentModel>> CreateOrUpdateToolAsync(string toolName, ToolDocumentModel model)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Creating or updating extended agent tool: {ToolName}, OperationId: {OperationId}", toolName, operationId);
            var tool = await _repository.UpsertToolAsync(model, operationId);
            await _extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();

            return new ApiCommandResult<ToolDocumentModel>(tool, operationId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while creating or updating extended agent tool: {ToolName}", toolName);
            throw;
        }
    }

    public async Task<ApiCommandResult<ToolDocumentModel>> DeleteToolAsync(string toolName)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Deleting extended agent tool: {ToolName}, OperationId: {OperationId}", toolName, operationId);

            var tool = await _repository.GetToolByNameAsync(toolName);
            if (tool == null)
            {
                return new ApiCommandResult<ToolDocumentModel>(new NoContentResult());
            }

            await _repository.DeleteToolAsync(toolName);
            await _extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();

            return new ApiCommandResult<ToolDocumentModel>(tool, operationId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while deleting extended agent tool: {ToolName}", toolName);
            throw;
        }
    }

    public async Task<ApiCommandResult<ToolDocumentModel>> GetToolAsync(string toolName)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Retrieving extended agent tool: {ToolName}, OperationId: {OperationId}", toolName, operationId);

            var tool = await _repository.GetToolByNameAsync(toolName);
            if (tool == null)
            {
                return new ApiCommandResult<ToolDocumentModel>(new NotFoundResult());
            }

            return new ApiCommandResult<ToolDocumentModel>(tool);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while retrieving extended agent tool: {ToolName}", toolName);
            throw;
        }
    }

    public async Task<ApiCommandResult<ToolDocumentModel[]>> GetToolsAsync(int limit = 50, string? search = null)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Getting extended agent tools, Limit: {Limit}, Search: {Search}, OperationId: {OperationId}", limit, search, operationId);

            var tools = await _repository.GetToolsAsync(limit, search);

            return new ApiCommandResult<ToolDocumentModel[]>(tools.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while listing extended agent tools");
            throw;
        }
    }

    // Connector operations
    public async Task<ApiCommandResult<ConnectorDocumentModel>> CreateOrUpdateConnectorAsync(string connectorName, ConnectorDocumentModel model)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Creating or updating extended agent connector: {ConnectorName}, OperationId: {OperationId}", connectorName, operationId);
            var connector = await _repository.UpsertConnectorAsync(model, operationId);
            await _extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();

            return new ApiCommandResult<ConnectorDocumentModel>(connector, operationId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while creating or updating extended agent connector: {ConnectorName}", connectorName);
            throw;
        }
    }

    public async Task<ApiCommandResult<ConnectorDocumentModel>> DeleteConnectorAsync(string connectorName)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Deleting extended agent connector: {ConnectorName}, OperationId: {OperationId}", connectorName, operationId);

            var connector = await _repository.GetConnectorByNameAsync(connectorName);
            if (connector == null)
            {
                return new ApiCommandResult<ConnectorDocumentModel>(new NoContentResult());
            }

            await _repository.DeleteConnectorAsync(connectorName);
            await _extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();

            return new ApiCommandResult<ConnectorDocumentModel>(connector, operationId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while deleting extended agent connector: {ConnectorName}", connectorName);
            throw;
        }
    }

    public async Task<ApiCommandResult<ConnectorDocumentModel>> GetConnectorAsync(string connectorName)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Retrieving extended agent connector: {ConnectorName}, OperationId: {OperationId}", connectorName, operationId);

            var connector = await _repository.GetConnectorByNameAsync(connectorName);
            if (connector == null)
            {
                return new ApiCommandResult<ConnectorDocumentModel>(new NotFoundResult());
            }

            return new ApiCommandResult<ConnectorDocumentModel>(connector);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while retrieving extended agent connector: {ConnectorName}", connectorName);
            throw;
        }
    }

    public async Task<ApiCommandResult<ConnectorDocumentModel[]>> GetConnectorsAsync(int limit = 50, string? search = null)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Getting extended agent connectors, Limit: {Limit}, Search: {Search}, OperationId: {OperationId}", limit, search, operationId);

            var connectors = await _repository.GetConnectorsAsync(limit, search);

            return new ApiCommandResult<ConnectorDocumentModel[]>(connectors.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while listing extended agent connectors");
            throw;
        }
    }

    // Plugin operations
    public Task<ApiCommandResult<PlugInConfigDocumentModel>> GetPluginConfigAsync(string pluginName)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Retrieving plugin config: {PluginName}, OperationId: {OperationId}", pluginName, operationId);

            // Note: Repository doesn't have GetPluginConfigByNameAsync, so we return NotFound for now
            // This might need to be implemented in the repository if needed
            return Task.FromResult(new ApiCommandResult<PlugInConfigDocumentModel>(new NotFoundResult()));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while retrieving plugin config: {PluginName}", pluginName);
            throw;
        }
    }

    public async Task<ApiCommandResult<PlugInConfigDocumentModel>> CreateOrUpdatePluginConfigAsync(string pluginName, PlugInConfigDocumentModel model)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Creating or updating plugin config: {PluginName}, OperationId: {OperationId}", pluginName, operationId);
            var plugin = await _repository.UpsertPluginConfigAsync(model);
            await _extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();

            return new ApiCommandResult<PlugInConfigDocumentModel>(plugin, operationId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while creating or updating plugin config: {PluginName}", pluginName);
            throw;
        }
    }

    public Task<ApiCommandResult<PlugInConfigDocumentModel>> DeletePluginConfigAsync(string pluginName)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Deleting plugin config: {PluginName}, OperationId: {OperationId}", pluginName, operationId);

            // Note: Repository doesn't have DeletePluginConfigAsync, so we return NotFound for now
            // This might need to be implemented in the repository if needed
            return Task.FromResult(new ApiCommandResult<PlugInConfigDocumentModel>(new NotFoundResult()));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while deleting plugin config: {PluginName}", pluginName);
            throw;
        }
    }

    public Task<ApiCommandResult<PlugInConfigDocumentModel[]>> GetPluginConfigsAsync(int limit = 50, string? search = null)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Getting plugin configs, Limit: {Limit}, Search: {Search}, OperationId: {OperationId}", limit, search, operationId);

            // Note: Repository doesn't have GetPluginConfigsAsync, so we return empty array for now
            // This might need to be implemented in the repository if needed
            return Task.FromResult(new ApiCommandResult<PlugInConfigDocumentModel[]>(Array.Empty<PlugInConfigDocumentModel>()));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while listing plugin configs");
            throw;
        }
    }

    // CommonPrompt operations
    public Task<ApiCommandResult<CommonPromptDocumentModel>> GetCommonPromptAsync(string promptName)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Retrieving common prompt: {PromptName}, OperationId: {OperationId}", promptName, operationId);

            // Note: Repository doesn't have GetCommonPromptByNameAsync, so we return NotFound for now
            // This might need to be implemented in the repository if needed
            return Task.FromResult(new ApiCommandResult<CommonPromptDocumentModel>(new NotFoundResult()));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while retrieving common prompt: {PromptName}", promptName);
            throw;
        }
    }

    public async Task<ApiCommandResult<CommonPromptDocumentModel>> CreateOrUpdateCommonPromptAsync(string promptName, CommonPromptDocumentModel model)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Creating or updating common prompt: {PromptName}, OperationId: {OperationId}", promptName, operationId);
            var prompt = await _repository.UpsertCommonPromptAsync(model, operationId);
            await _extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();

            return new ApiCommandResult<CommonPromptDocumentModel>(prompt, operationId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while creating or updating common prompt: {PromptName}", promptName);
            throw;
        }
    }

    public Task<ApiCommandResult<CommonPromptDocumentModel>> DeleteCommonPromptAsync(string promptName)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Deleting common prompt: {PromptName}, OperationId: {OperationId}", promptName, operationId);

            // Note: Repository doesn't have DeleteCommonPromptAsync, so we return NotFound for now
            // This might need to be implemented in the repository if needed
            return Task.FromResult(new ApiCommandResult<CommonPromptDocumentModel>(new NotFoundResult()));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while deleting common prompt: {PromptName}", promptName);
            throw;
        }
    }

    public async Task<ApiCommandResult<CommonPromptDocumentModel[]>> GetCommonPromptsAsync(int limit = 50, string? search = null)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Getting common prompts, Limit: {Limit}, Search: {Search}, OperationId: {OperationId}", limit, search, operationId);

            var prompts = await _repository.GetCommonPromptsAsync(limit, search);

            return new ApiCommandResult<CommonPromptDocumentModel[]>(prompts.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while listing common prompts");
            throw;
        }
    }

    // CommonToolList operations
    public Task<ApiCommandResult<CommonToolsListDocumentModel>> GetCommonToolListAsync(string listName)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Retrieving common tool list: {ListName}, OperationId: {OperationId}", listName, operationId);

            // Note: Repository doesn't have GetCommonToolListByNameAsync, so we return NotFound for now
            // This might need to be implemented in the repository if needed
            return Task.FromResult(new ApiCommandResult<CommonToolsListDocumentModel>(new NotFoundResult()));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while retrieving common tool list: {ListName}", listName);
            throw;
        }
    }

    public async Task<ApiCommandResult<CommonToolsListDocumentModel>> CreateOrUpdateCommonToolListAsync(string listName, CommonToolsListDocumentModel model)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Creating or updating common tool list: {ListName}, OperationId: {OperationId}", listName, operationId);
            var toolList = await _repository.UpsertCommonToolsListAsync(model, operationId);
            await _extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();

            return new ApiCommandResult<CommonToolsListDocumentModel>(toolList, operationId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while creating or updating common tool list: {ListName}", listName);
            throw;
        }
    }

    public Task<ApiCommandResult<CommonToolsListDocumentModel>> DeleteCommonToolListAsync(string listName)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Deleting common tool list: {ListName}, OperationId: {OperationId}", listName, operationId);

            // Note: Repository doesn't have DeleteCommonToolListAsync, so we return NotFound for now
            // This might need to be implemented in the repository if needed
            return Task.FromResult(new ApiCommandResult<CommonToolsListDocumentModel>(new NotFoundResult()));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while deleting common tool list: {ListName}", listName);
            throw;
        }
    }

    public async Task<ApiCommandResult<CommonToolsListDocumentModel[]>> GetCommonToolListsAsync(int limit = 50, string? search = null)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Getting common tool lists, Limit: {Limit}, Search: {Search}, OperationId: {OperationId}", limit, search, operationId);

            var toolLists = await _repository.GetCommonToolsListsAsync(limit, search);

            return new ApiCommandResult<CommonToolsListDocumentModel[]>(toolLists.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while listing common tool lists");
            throw;
        }
    }
}