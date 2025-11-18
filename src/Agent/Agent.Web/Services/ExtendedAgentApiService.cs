// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Data.DataModels;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Services;
using Agent.Web.ApiResources;
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

    public ExtendedAgentApiService(
        ILogger<ExtendedAgentApiService> logger,
        IExtendedAgentService extendedAgentService,
        IExtendedAgentRepository repository,
        IExtendedAgentValidator validator,
        AgentToSkillService agentToSkillService
    )
    {
        _logger = logger;
        _extendedAgentService = extendedAgentService;
        _repository = repository;
        _validator = validator;
        _agentToSkillService = agentToSkillService;
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
}
