// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using Agent.Core.Interfaces;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Plugins.Connector;
using Agent.Plugins.Kusto;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Agent.Runtime.Services;
using Agent.Web.ApiResources;
using Agent.Web.Models.ExtendedAgents;
using Agent.Web.Models.ExtendedAgents.Request;
using Agent.Web.Models.ExtendedAgents.Response;
using Agent.Web.Validation;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Services;

public class ExtendedAgentApiService : IExtendedAgentApiService
{
    private readonly ILogger<ExtendedAgentApiService> _logger;
    private readonly IExtendedAgentService _extendedAgentService;
    private readonly IExtendedAgentRepository _repository;
    private readonly IExtendedAgentValidator _validator;
    private readonly AgentToSkillService _agentToSkillService;
    private readonly IAuthenticationService _authService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IMcpConnectionEventManager _mcpConnectionManager;
    private readonly IConnectorResolver _connectorResolver;
    private readonly IIncidentFilterManagementServiceFactory _incidentFilterManagementServiceFactory;

    public ExtendedAgentApiService(
        ILogger<ExtendedAgentApiService> logger,
        IExtendedAgentService extendedAgentService,
        IExtendedAgentRepository repository,
        IExtendedAgentValidator validator,
        AgentToSkillService agentToSkillService,
        IAuthenticationService authService,
        ILoggerFactory loggerFactory,
        IMcpConnectionEventManager mcpConnectionEventManager,
        IConnectorResolver connectorResolver,
        IIncidentFilterManagementServiceFactory incidentFilterManagementServiceFactory)
    {
        _logger = logger;
        _extendedAgentService = extendedAgentService;
        _repository = repository;
        _validator = validator;
        _agentToSkillService = agentToSkillService;
        _authService = authService;
        _loggerFactory = loggerFactory;
        _mcpConnectionManager = mcpConnectionEventManager;
        _connectorResolver = connectorResolver;
        _incidentFilterManagementServiceFactory = incidentFilterManagementServiceFactory;
    }

    public async Task<ApiCommandResult<AgentDocumentModel>> CreateOrUpdateAgentAsync(string agentName, AgentDocumentModel model, bool dryRun = false)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Creating or updating extended agent: {AgentName}, DryRun: {DryRun}, OperationId: {OperationId}", agentName, dryRun, operationId);

            // Validate the model
            var validationResult = await _validator.ValidateAgentAsync(model);
            if (!validationResult.IsValid)
            {
                _logger.LogInternalWarning("Validation failed for agent: {AgentName}. Errors: {Errors}", agentName, string.Join(", ", validationResult.Errors));
                return new ApiCommandResult<AgentDocumentModel>(new BadRequestObjectResult(ErrorMap.ValidationFailure.CreateErrorEntity(validationResult.ToString())));
            }

            // If dry-run, skip database operations and return the validated model
            if (dryRun)
            {
                _logger.LogInternalInformation("Dry-run mode: Skipping database operations for agent: {AgentName}", agentName);
                return new ApiCommandResult<AgentDocumentModel>(model, operationId);
            }

            // Perform actual database operations
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

    public async Task<ApiCommandResult<AgentDocumentModel>> DeleteAgentAsync(string agentName, bool dryRun = false)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Deleting extended agent: {AgentName}, DryRun: {DryRun}, OperationId: {OperationId}", agentName, dryRun, operationId);

            var agent = await _repository.GetAgentByNameAsync(agentName);
            if (agent == null)
            {
                return new ApiCommandResult<AgentDocumentModel>(new NoContentResult());
            }

            // If dry-run, skip database operations and return the agent that would be deleted
            if (dryRun)
            {
                _logger.LogInternalInformation("Dry-run mode: Skipping delete operation for agent: {AgentName}", agentName);
                return new ApiCommandResult<AgentDocumentModel>(agent, operationId);
            }

            // Perform actual delete operation
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
    public async Task<ApiCommandResult<ToolDocumentModel>> CreateOrUpdateToolAsync(string toolName, ToolDocumentModel model, bool dryRun = false)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Creating or updating extended agent tool: {ToolName}, DryRun: {DryRun}, OperationId: {OperationId}", toolName, dryRun, operationId);

            // Validate the model
            var validationResult = await _validator.ValidateToolAsync(model);
            if (!validationResult.IsValid)
            {
                _logger.LogInternalWarning("Validation failed for tool: {ToolName}. Errors: {Errors}", toolName, string.Join(", ", validationResult.Errors));
                return new ApiCommandResult<ToolDocumentModel>(new BadRequestObjectResult(ErrorMap.ValidationFailure.CreateErrorEntity(validationResult.Errors)));
            }

            // If dry-run, skip database operations and return the validated model
            if (dryRun)
            {
                _logger.LogInternalInformation("Dry-run mode: Skipping database operations for tool: {ToolName}", toolName);
                return new ApiCommandResult<ToolDocumentModel>(model, operationId);
            }

            // Perform actual database operations
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

    public async Task<ApiCommandResult<ToolDocumentModel>> DeleteToolAsync(string toolName, bool dryRun = false)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Deleting extended agent tool: {ToolName}, DryRun: {DryRun}, OperationId: {OperationId}", toolName, dryRun, operationId);

            var tool = await _repository.GetToolByNameAsync(toolName);
            if (tool == null)
            {
                return new ApiCommandResult<ToolDocumentModel>(new NoContentResult());
            }

            // If dry-run, skip database operations and return the tool that would be deleted
            if (dryRun)
            {
                _logger.LogInternalInformation("Dry-run mode: Skipping delete operation for tool: {ToolName}", toolName);
                return new ApiCommandResult<ToolDocumentModel>(tool, operationId);
            }

            // Perform actual delete operations
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
    public async Task<ApiCommandResult<ConnectorDocumentModel>> CreateOrUpdateConnectorAsync(string connectorName, ConnectorDocumentModel model, bool dryRun = false)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Creating or updating extended agent connector: {ConnectorName}, DryRun: {DryRun}, OperationId: {OperationId}", connectorName, dryRun, operationId);

            // Validate the model
            var validationResult = await _validator.ValidateConnectorAsync(model);
            if (!validationResult.IsValid)
            {
                _logger.LogInternalWarning("Validation failed for connector: {ConnectorName}. Errors: {Errors}", connectorName, string.Join(", ", validationResult.Errors));
                return new ApiCommandResult<ConnectorDocumentModel>(new BadRequestObjectResult(ErrorMap.ValidationFailure.CreateErrorEntity(validationResult.Errors)));
            }

            // If dry-run, skip database operations and return the validated model
            if (dryRun)
            {
                _logger.LogInternalInformation("Dry-run mode: Skipping database operations for connector: {ConnectorName}", connectorName);
                return new ApiCommandResult<ConnectorDocumentModel>(model, operationId);
            }

            // Perform actual database operations
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

    public async Task<ApiCommandResult<ConnectorDocumentModel>> DeleteConnectorAsync(string connectorName, bool dryRun = false)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Deleting extended agent connector: {ConnectorName}, DryRun: {DryRun}, OperationId: {OperationId}", connectorName, dryRun, operationId);

            var connector = await _repository.GetConnectorByNameAsync(connectorName);
            if (connector == null)
            {
                return new ApiCommandResult<ConnectorDocumentModel>(new NoContentResult());
            }

            // If dry-run, skip database operations and return the connector that would be deleted
            if (dryRun)
            {
                _logger.LogInternalInformation("Dry-run mode: Skipping delete operation for connector: {ConnectorName}", connectorName);
                return new ApiCommandResult<ConnectorDocumentModel>(connector, operationId);
            }

            // Perform actual delete operations
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

    public async Task<ApiCommandResult<PlugInConfigDocumentModel>> CreateOrUpdatePluginConfigAsync(string pluginName, PlugInConfigDocumentModel model, bool dryRun = false)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Creating or updating plugin config: {PluginName}, DryRun: {DryRun}, OperationId: {OperationId}", pluginName, dryRun, operationId);

            // Validate the model
            var validationResult = await _validator.ValidatePluginConfigAsync(model);
            if (!validationResult.IsValid)
            {
                _logger.LogInternalWarning("Validation failed for plugin config: {PluginName}. Errors: {Errors}", pluginName, string.Join(", ", validationResult.Errors));
                return new ApiCommandResult<PlugInConfigDocumentModel>(new BadRequestObjectResult(ErrorMap.ValidationFailure.CreateErrorEntity(validationResult.Errors)));
            }

            // If dry-run, skip database operations and return the validated model
            if (dryRun)
            {
                _logger.LogInternalInformation("Dry-run mode: Skipping database operations for plugin config: {PluginName}", pluginName);
                return new ApiCommandResult<PlugInConfigDocumentModel>(model, operationId);
            }

            // Perform actual database operations
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

    public Task<ApiCommandResult<PlugInConfigDocumentModel>> DeletePluginConfigAsync(string pluginName, bool dryRun = false)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Deleting plugin config: {PluginName}, DryRun: {DryRun}, OperationId: {OperationId}", pluginName, dryRun, operationId);

            // Note: Repository doesn't have DeletePluginConfigAsync, so we return NotFound for now
            // This might need to be implemented in the repository if needed
            // When implemented, add dry-run logic similar to other delete methods
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
    public async Task<ApiCommandResult<CommonPromptDocumentModel>> GetCommonPromptAsync(string promptName)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Retrieving common prompt: {PromptName}, OperationId: {OperationId}", promptName, operationId);

            var prompt = await _repository.GetCommonPromptByNameAsync(promptName);
            if (prompt == null)
            {
                return new ApiCommandResult<CommonPromptDocumentModel>(new NotFoundResult());
            }

            return new ApiCommandResult<CommonPromptDocumentModel>(prompt);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while retrieving common prompt: {PromptName}", promptName);
            throw;
        }
    }

    public async Task<ApiCommandResult<CommonPromptDocumentModel>> CreateOrUpdateCommonPromptAsync(string promptName, CommonPromptDocumentModel model, bool dryRun = false)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Creating or updating common prompt: {PromptName}, DryRun: {DryRun}, OperationId: {OperationId}", promptName, dryRun, operationId);

            // Validate the model
            var validationResult = await _validator.ValidateCommonPromptAsync(model);
            if (!validationResult.IsValid)
            {
                _logger.LogInternalWarning("Validation failed for common prompt: {PromptName}. Errors: {Errors}", promptName, string.Join(", ", validationResult.Errors));
                return new ApiCommandResult<CommonPromptDocumentModel>(new BadRequestObjectResult(ErrorMap.ValidationFailure.CreateErrorEntity(validationResult.Errors)));
            }

            // If dry-run, skip database operations and return the validated model
            if (dryRun)
            {
                _logger.LogInternalInformation("Dry-run mode: Skipping database operations for common prompt: {PromptName}", promptName);
                return new ApiCommandResult<CommonPromptDocumentModel>(model, operationId);
            }

            // Perform actual database operations
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

    public Task<ApiCommandResult<CommonPromptDocumentModel>> DeleteCommonPromptAsync(string promptName, bool dryRun = false)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Deleting common prompt: {PromptName}, DryRun: {DryRun}, OperationId: {OperationId}", promptName, dryRun, operationId);

            // Note: Repository doesn't have DeleteCommonPromptAsync, so we return NotFound for now
            // This might need to be implemented in the repository if needed
            // When implemented, add dry-run logic similar to other delete methods
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

    public async Task<ApiCommandResult<CommonToolsListDocumentModel>> CreateOrUpdateCommonToolListAsync(string listName, CommonToolsListDocumentModel model, bool dryRun = false)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Creating or updating common tool list: {ListName}, DryRun: {DryRun}, OperationId: {OperationId}", listName, dryRun, operationId);

            // Validate the model
            var validationResult = await _validator.ValidateCommonToolsListAsync(model);
            if (!validationResult.IsValid)
            {
                _logger.LogInternalWarning("Validation failed for common tool list: {ListName}. Errors: {Errors}", listName, string.Join(", ", validationResult.Errors));
                return new ApiCommandResult<CommonToolsListDocumentModel>(new BadRequestObjectResult(ErrorMap.ValidationFailure.CreateErrorEntity(validationResult.Errors)));
            }

            // If dry-run, skip database operations and return the validated model
            if (dryRun)
            {
                _logger.LogInternalInformation("Dry-run mode: Skipping database operations for common tool list: {ListName}", listName);
                return new ApiCommandResult<CommonToolsListDocumentModel>(model, operationId);
            }

            // Perform actual database operations
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

    public Task<ApiCommandResult<CommonToolsListDocumentModel>> DeleteCommonToolListAsync(string listName, bool dryRun = false)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Deleting common tool list: {ListName}, DryRun: {DryRun}, OperationId: {OperationId}", listName, dryRun, operationId);

            // Note: Repository doesn't have DeleteCommonToolListAsync, so we return NotFound for now
            // This might need to be implemented in the repository if needed
            // When implemented, add dry-run logic similar to other delete methods
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

    // Skill operations
    public async Task<ApiCommandResult<SkillDocumentModel>> CreateOrUpdateSkillAsync(string skillName, SkillDocumentModel model)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Creating or updating skill: {SkillName}, OperationId: {OperationId}", skillName, operationId);
            var skill = await _repository.UpsertSkillDocumentAsync(model, operationId);
            await _extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();

            return new ApiCommandResult<SkillDocumentModel>(skill, operationId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while creating or updating skill: {SkillName}", skillName);
            throw;
        }
    }

    public async Task<ApiCommandResult<SkillDocumentModel>> DeleteSkillAsync(string skillName)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Deleting skill: {SkillName}, OperationId: {OperationId}", skillName, operationId);

            var skill = await _repository.GetSkillByNameAsync(skillName);
            if (skill == null)
            {
                return new ApiCommandResult<SkillDocumentModel>(new NoContentResult());
            }

            await _repository.DeleteSkillAsync(skillName);
            await _extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();

            return new ApiCommandResult<SkillDocumentModel>(skill, operationId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while deleting skill: {SkillName}", skillName);
            throw;
        }
    }

    public async Task<ApiCommandResult<SkillDocumentModel>> GetSkillAsync(string skillName)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Retrieving skill: {SkillName}, OperationId: {OperationId}", skillName, operationId);

            var skill = await _repository.GetSkillByNameAsync(skillName);
            if (skill == null)
            {
                return new ApiCommandResult<SkillDocumentModel>(new NotFoundResult());
            }

            return new ApiCommandResult<SkillDocumentModel>(skill);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while retrieving skill: {SkillName}", skillName);
            throw;
        }
    }

    public async Task<ApiCommandResult<SkillDocumentModel[]>> GetSkillsAsync(int limit = 50, string? search = null)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Getting skills, Limit: {Limit}, Search: {Search}, OperationId: {OperationId}", limit, search, operationId);

            var skills = await _repository.GetSkillsAsync(limit, search);

            return new ApiCommandResult<SkillDocumentModel[]>(skills.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while listing skills");
            throw;
        }
    }

    public async Task<ApiCommandResult<SkillDocumentModel>> ConvertAgentToSkillAsync(string agentName, List<string> topLevelAgents)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInternalInformation("Converting agent to skill: {AgentName}, OperationId: {OperationId}", agentName, operationId);

            // Check if agent exists
            var agent = await _repository.GetAgentByNameAsync(agentName);
            if (agent == null)
            {
                return new ApiCommandResult<SkillDocumentModel>(new NotFoundResult());
            }

            // Convert agent to skill
            var skill = await _agentToSkillService.ConvertAgentToSkillAsync(agentName, topLevelAgents);

            // Create skill document model
            var skillDocumentModel = new SkillDocumentModel(
                Metadata: new ResourceMetadata
                {
                    Id = $"skill_{skill.Name}",
                    CreatedAt = DateTime.UtcNow,
                    Version = "v2",
                },
                Spec: skill
            );

            // Save skill to database
            var createdSkill = await _repository.UpsertSkillDocumentAsync(skillDocumentModel, operationId);
            await _extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();

            _logger.LogInternalInformation("Skill '{SkillName}' created successfully from agent '{AgentName}'", skill.Name, agentName);

            return new ApiCommandResult<SkillDocumentModel>(createdSkill, operationId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while converting agent to skill: {AgentName}", agentName);
            throw;
        }
    }

    // Helper: MCP connector status
    private ConnectorStatusResponse BuildMcpConnectorStatus(string connectorName)
    {
        var mcpActive = _mcpConnectionManager.GetActiveConnections()
            .FirstOrDefault(c => string.Equals(c.Id, connectorName, StringComparison.OrdinalIgnoreCase));
        if (mcpActive == null)
        {
            return new ConnectorStatusResponse(
                Name: connectorName,
                Type: "Mcp",
                Healthy: false,
                Message: "No active MCP connection found.",
                Status: DataConnectorStatus.Disconnected.ToString(),
                ExecutionTimeMs: 0,
                Details: null);
        }

        // Healthy means Connected or Standby (ready to use)
        var healthy = mcpActive.Status == DataConnectorStatus.Connected || mcpActive.Status == DataConnectorStatus.Standby;
        var message = mcpActive.Status switch
        {
            DataConnectorStatus.Connected => "MCP connection established.",
            DataConnectorStatus.Standby => "MCP connection established.", // User sees "Connected", internally we track Standby for refresh
            _ => $"MCP connection status: {mcpActive.Status}. {mcpActive.ErrorMessage}"
        };

        var userFacingStatus = mcpActive.Status == DataConnectorStatus.Standby
            ? DataConnectorStatus.Connected.ToString()
            : mcpActive.Status.ToString();

        return new ConnectorStatusResponse(
            Name: connectorName,
            Type: "Mcp",
            Healthy: healthy,
            Message: message,
            Status: userFacingStatus,
            ExecutionTimeMs: 0,
            Details: new
            {
                error = mcpActive.ErrorMessage,
                tools = mcpActive.Tools?.Count ?? 0,
                lastHeartbeat = mcpActive.LastHeartbeat
            });
    }

    // Helper: Kusto connector status using TestKustoQueryAsync
    private async Task<ConnectorStatusResponse> BuildKustoConnectorStatusAsync(string connectorName)
    {
        string standardQuery = "union * | take 1";
        var testRequest = new KustoQueryTestRequest
        {
            Query = standardQuery,
            Connector = connectorName,
            Database = string.Empty, // use default
            Parameters = new Dictionary<string, string>()
        };
        var testResult = await TestKustoQueryAsync("kusto", testRequest, CancellationToken.None);

        var healthy = testResult.Success;
        var message = healthy
            ? (testResult.RowCount > 0 ? "Kusto connectivity OK. Sample row retrieved." : "Kusto connectivity OK. No rows returned.")
            : ($"Kusto connectivity failed: {testResult.ErrorMessage}");

        return new ConnectorStatusResponse(
            Name: connectorName,
            Type: "Kusto",
            Healthy: healthy,
            Message: message,
            Status: healthy ? DataConnectorStatus.Connected.ToString() : DataConnectorStatus.Failed.ToString(),
            ExecutionTimeMs: testResult.ExecutionTimeMs,
            Details: healthy ? new { sampleRow = testResult.RowCount > 0, rowCount = testResult.RowCount, query = standardQuery } : null);
    }

    // Helper: ICM connector status using CheckConnectivity
    private async Task<ConnectorStatusResponse> BuildIcmConnectorStatusAsync(string connectorName)
    {
        var stopwatch = Stopwatch.StartNew();
        bool healthy = false;
        string message = "Unable to determine status";
        object? details = null;

        try
        {
            var service = _incidentFilterManagementServiceFactory.GetServiceDynamic();
            healthy = await service.CheckConnectivity();
            message = healthy ? "ICM connectivity OK." : "ICM connectivity failed.";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "BuildIcmConnectorStatusAsync: Exception occurred while checking ICM connectivity for connector: {ConnectorName}", connectorName);
            message = $"ICM connectivity check failed: {ex.Message}";
            details = new { error = ex.Message };
        }
        finally
        {
            stopwatch.Stop();
        }        

        return new ConnectorStatusResponse(
            Name: connectorName,
            Type: "Icm",
            Healthy: healthy,
            Message: message,
            Status: healthy ? DataConnectorStatus.Connected.ToString() : DataConnectorStatus.Failed.ToString(),
            ExecutionTimeMs: stopwatch.ElapsedMilliseconds,
            Details: details);
    }

    public async Task<ApiCommandResult<ConnectorStatusResponse>> GetConnectorStatusAsync(string connectorName)
    {
        try
        {
            var all = _connectorResolver.GetAllDataConnectors();
            var connector = all.FirstOrDefault(c => c.Name.Equals(connectorName, StringComparison.OrdinalIgnoreCase));

            if (connector == null)
            {
                return new ApiCommandResult<ConnectorStatusResponse>(new ConnectorStatusResponse(
                    Name: connectorName,
                    Type: "Unknown",
                    Healthy: false,
                    Message: "Connector is being initialized. This may take a few minutes...",
                    Status: DataConnectorStatus.Initializing.ToString(),
                    ExecutionTimeMs: 0,
                    Details: null));
            }

            ConnectorStatusResponse response = connector.ConnectorType.ToLowerInvariant() switch
            {
                "mcp" => BuildMcpConnectorStatus(connectorName),
                "kusto" => await BuildKustoConnectorStatusAsync(connectorName),
                "icm" => await BuildIcmConnectorStatusAsync(connectorName),
                _ => new ConnectorStatusResponse(
                        Name: connectorName,
                        Type: connector.ConnectorType,
                        Healthy: false,
                        Message: $"Status not available for connector type '{connector.ConnectorType}'.",
                        Status: DataConnectorStatus.Failed.ToString(),
                        ExecutionTimeMs: 0,
                        Details: null)
            };

            return new ApiCommandResult<ConnectorStatusResponse>(response);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Unexpected error while retrieving connector status: {ConnectorName}", connectorName);
            throw;
        }
    }

    public async Task<KustoQueryTestResponse> TestKustoQueryAsync(string toolName, KustoQueryTestRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate tool name - only Kusto is supported for now
            if (!toolName.Equals("kusto", StringComparison.OrdinalIgnoreCase))
            {
                return new KustoQueryTestResponse
                {
                    Success = false,
                    ErrorMessage = $"Tool type '{toolName}' is not supported. Only 'kusto' is currently supported.",
                    QueryExecuted = request.Query
                };
            }

            // Validate request
            if (string.IsNullOrWhiteSpace(request.Query) ||
                string.IsNullOrWhiteSpace(request.Connector))
            {
                return new KustoQueryTestResponse
                {
                    Success = false,
                    ErrorMessage = "Query and Connector are required",
                    QueryExecuted = request.Query
                };
            }

            // Get the connector using resolver
            var connector = _connectorResolver.GetConnectorFromSettings<KustoConnector>(
                request.Connector,
                "kusto",
                dataSource: string.Empty);

            // Use default database
            if (string.IsNullOrEmpty(request.Database))
            {
                request.Database = connector.Database;
            }

            // Substitute parameters in the query
            var processedQuery = request.Query;
            foreach (var param in request.Parameters)
            {
                var placeholder = $"##{param.Key}##";
                processedQuery = processedQuery.Replace(placeholder, param.Value, StringComparison.OrdinalIgnoreCase);
            }

            // Enforce limit by appending "| take 50" if not already present
            var trimmedQuery = processedQuery.Trim();
            if (!trimmedQuery.Contains("| take", StringComparison.OrdinalIgnoreCase))
            {
                processedQuery = $"{processedQuery}\n| take 50";
            }

            // Create KustoClient and execute the query
            var kustoLogger = _loggerFactory.CreateLogger<KustoClient>();
            var kustoClient = new KustoClient(kustoLogger, connector, _authService);

            var startTime = DateTime.UtcNow;
            var queryResult = await kustoClient.ExecuteClusterKustoQuery(
                connector.ClusterUrl,
                request.Database,
                processedQuery,
                cancellationToken);
            var executionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

            // Parse the result string to extract columns and rows
            var columns = new List<string>();
            var rows = new List<Dictionary<string, object>>();

            if (!string.IsNullOrWhiteSpace(queryResult.Result))
            {
                var lines = queryResult.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0)
                {
                    // First line contains column names
                    columns = lines[0].Split('\t').ToList();

                    // Remaining lines are data rows (limit to 50 rows + header ignored)
                    for (int i = 1; i < Math.Min(lines.Length, 51); i++)
                    {
                        var values = lines[i].Split('\t');
                        var rowDict = new Dictionary<string, object>();
                        for (int j = 0; j < Math.Min(columns.Count, values.Length); j++)
                        {
                            rowDict[columns[j]] = values[j];
                        }
                        rows.Add(rowDict);
                    }
                }
            }

            return new KustoQueryTestResponse
            {
                Success = queryResult.Success,
                RowCount = queryResult.RowCount,
                Columns = columns,
                Rows = rows,
                ExecutionTimeMs = executionTime,
                QueryExecuted = processedQuery,
                ErrorMessage = queryResult.Success ? null : queryResult.Result
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Kusto test execution failed for tool {ToolName}", toolName);
            return new KustoQueryTestResponse
            {
                Success = false,
                RowCount = 0,
                Columns = new List<string>(),
                Rows = new List<Dictionary<string, object>>(),
                ExecutionTimeMs = 0,
                QueryExecuted = request.Query,
                ErrorMessage = ex.Message
            };
        }
    }
}
