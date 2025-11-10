// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models.ExtendedAgents;
using Agent.Web.Models.ExtendedAgents;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Agent.Web.Services;

public static class PluginConfigApplier
{
    public static void Apply<T>(PluginConfigDeploymentModel deployment, Type targetSettingsType, IReloadableSettingsStore store, ReloadableOptionsChangeTokenSource<T> changeTokenSource)
    {
        if (deployment == null)
            throw new ArgumentNullException(nameof(deployment));
        if (targetSettingsType == null)
            throw new ArgumentNullException(nameof(targetSettingsType));
        if (store == null)
            throw new ArgumentNullException(nameof(store));

        // Deserialize the config dictionary into a strongly typed instance
        var json = JsonConvert.SerializeObject(deployment.Spec.Config);

        var settingsInstance = JsonConvert.DeserializeObject(json, targetSettingsType);
        if (settingsInstance == null)
            throw new InvalidOperationException($"Failed to create settings for plugin '{deployment.Spec.PluginName}'");

        store.Set(deployment.Spec.PluginName, settingsInstance);
        changeTokenSource.TriggerReload();
    }
}

public class ResourceDeploymentService : IResourceDeploymentService
{
    private readonly IExtendedAgentRepository _repository;
    private readonly IExtendedAgentService _extendedAgentService;
    private readonly IPluginSettingsTypeRegistry _pluginSettingsTypeRegistry;
    private readonly IReloadableSettingsStore _settingsStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ResourceDeploymentService> _logger;

    public ResourceDeploymentService(
        IExtendedAgentRepository repository,
        IExtendedAgentService extendedAgentService,
        IReloadableSettingsStore settingsStore,
        IPluginSettingsTypeRegistry pluginSettingsTypeRegistry,
        IServiceProvider serviceProvider,
        ILogger<ResourceDeploymentService> logger)
    {
        _repository = repository;
        _extendedAgentService = extendedAgentService;
        _pluginSettingsTypeRegistry = pluginSettingsTypeRegistry;
        _settingsStore = settingsStore;
        _serviceProvider = serviceProvider;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    }

    public async Task<IActionResult> ApplyAsync(AgentDeploymentModel spec)
    {
        if (spec == null)
        {
            throw new ArgumentNullException(nameof(spec));
        }

        if (spec.Spec == null)
        {
            throw new ArgumentNullException(nameof(spec.Spec));
        }

        var agentSpec = spec.Spec;
        var agentName = agentSpec.Name;
        if (string.IsNullOrWhiteSpace(agentName))
        {
            throw new ArgumentException("Agent name cannot be null or empty", nameof(agentSpec.Name));
        }

        var operationId = Guid.NewGuid().ToString();
        _logger.LogInternalInformation("Starting agent apply for {AgentName} with operation id {OperationId}", agentName, operationId);

        try
        {
            var currentTime = DateTime.UtcNow;

            // Check if the agent already exists to determine CreatedAt value
            var existingAgent = await _repository.GetAgentByNameAsync(agentName);

            // Update metadata with timestamps
            var yamlMetadata = spec.Metadata ?? new YamlMetadata();
            yamlMetadata.UpdatedAt = currentTime;

            // Only set CreatedAt if this is a new agent
            if (existingAgent == null)
            {
                yamlMetadata.CreatedAt = currentTime;
            }
            else
            {
                // Preserve existing CreatedAt timestamp
                yamlMetadata.CreatedAt = existingAgent.Metadata?.CreatedAt ?? currentTime;
            }

            // Map agent
            var agentSpecModel = new AgentSpec
            {
                Name = agentName,
                Instructions = agentSpec.Instructions ?? string.Empty,
                HandoffDescription = agentSpec.HandoffDescription,
                Handoffs = agentSpec.Handoffs,
                Tools = agentSpec.Tools,
                McpTools = agentSpec.McpTools,
                Connectors = agentSpec.Connectors,
                AllowParallelToolCalls = agentSpec.AllowParallelToolCalls,
                AgentsAsTools = agentSpec.AgentsAsTools,
                MaxReflectionCount = agentSpec.MaxReflectionCount,
                CriticPromptPath = agentSpec.CriticPromptPath,
                CriticOnHandOff = agentSpec.CriticOnHandOff,
                CustomReflectionNote = agentSpec.CustomReflectionNote,
                CommonPrompts = agentSpec.CommonPrompts,
                CommonTools = agentSpec.CommonTools,
                DisableDocumentRetrieval = agentSpec.DisableDocumentRetrieval,
                EnableHandoffPromptOverride = agentSpec.EnableHandoffPromptOverride,
                HandoffPromptOverride = agentSpec.HandoffPromptOverride,
                UserPromptOverride = agentSpec.UserPromptOverride,
                InstructionsOverride = agentSpec.InstructionsOverride,
                Temperature = agentSpec.Temperature,
                LlmModelName = agentSpec.LlmModelName,
                EnableVanillaMode = agentSpec.EnableVanillaMode,

                // Workflow agent properties
                AgentType = agentSpec.AgentType,
                ParameterExtractionAgent = agentSpec.ParameterExtractionAgent,
                OrchestrationStartAgents = agentSpec.OrchestrationStartAgents,
                ResultSummarizationPrompt = agentSpec.ResultSummarizationPrompt,
                NextAgentMappings = agentSpec.NextAgentMappings,
                OutputType = agentSpec.OutputType
            };

            var metadata = ResourceMetadata.FromYamlMetadata(yamlMetadata, agentName, operationId);
            var agentDoc = new AgentDocumentModel(
                Metadata: metadata,
                Spec: agentSpecModel
            );

            // Persist agent to Cosmos (tools and connectors are already referenced by name in the agent document)
            await _repository.UpsertAgentAsync(agentDoc, operationId);
            await _extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();
            var result = new ExtendedAgentApply
            {
                Status = ExtendedAgentApplyStatus.Accepted,
                Message = "Agent and tools deployment initiated",
                OperationId = "",
                Timestamp = DateTime.UtcNow,
                Details = new ExtendedAgentApplyDetails
                {
                    AgentName = agentName,
                    ToolsCount = agentSpec.Tools?.Count ?? 0,
                    ConnectorsCount = agentSpec.Connectors?.Count ?? 0
                }
            };
            _logger.LogInternalInformation("Agent apply succeeded for {AgentName} with operation id {OperationId}", agentName, operationId);
            return new OkObjectResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Agent apply failed for {AgentName} with operation id {OperationId}", agentName, operationId);
            throw;
        }
    }

    public async Task<IActionResult> ApplyAsync(ConnectorsDeploymentModel spec)
    {
        var operationId = Guid.NewGuid().ToString();

        var connectorDocs = spec.Spec.Connectors.Select(c =>
                ApiToRuntimeMapper.ToDocumentConnector(c, operationId));


        foreach (var connector in connectorDocs)
            await _repository.UpsertConnectorAsync(connector, operationId);

        await _extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();
        var result = new ExtendedAgentApply
        {
            Status = ExtendedAgentApplyStatus.Accepted,
            Message = "Agent and tools deployment initiated",
            OperationId = "",
            Timestamp = DateTime.UtcNow,
            Details = new ExtendedAgentApplyDetails
            {
                ConnectorsCount = spec.Spec.Connectors.Count,
            }
        };
        return new OkObjectResult(result);
    }

    public async Task<IActionResult> ApplyAsync(ToolsDeploymentModel spec)
    {
        var operationId = Guid.NewGuid().ToString();

        // Map tools
        var toolDocs = spec.Spec.Tools.Select(t =>
             ApiToRuntimeMapper.ToDocumentTool(t, operationId));

        if (toolDocs == null || !toolDocs.Any())
        {
            return new BadRequestObjectResult("No tools provided in the deployment model.");
        }
        foreach (var tool in toolDocs)
            await _repository.UpsertToolAsync(tool, operationId);

        await _extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();
        var result = new ExtendedAgentApply
        {
            Status = ExtendedAgentApplyStatus.Accepted,
            Message = "Agent and tools deployment initiated",
            OperationId = "",
            Timestamp = DateTime.UtcNow,
            Details = new ExtendedAgentApplyDetails
            {
                ToolsCount = spec.Spec.Tools.Count,
            }
        };
        return new OkObjectResult(result);
    }

    public async Task<IActionResult> ApplyAsync(CommonToolsListDeploymentModel spec)
    {
        var operationId = Guid.NewGuid().ToString();

        // Map tools
        var commonTools = spec.Spec.CommonToolsLists.Select(t =>
             ApiToRuntimeMapper.ToCommonToolsList(t, operationId));

        if (commonTools == null || !commonTools.Any())
        {
            return new BadRequestObjectResult("No prompt provided in the deployment model.");
        }
        foreach (var toolList in commonTools)
            await _repository.UpsertCommonToolsListAsync(toolList, operationId);

        await _extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();
        var result = new ExtendedAgentApply
        {
            Status = ExtendedAgentApplyStatus.Accepted,
            Message = "Common tools list deployment initiated",
            OperationId = "",
            Timestamp = DateTime.UtcNow,
            Details = new ExtendedAgentApplyDetails
            {
                ToolsCount = spec.Spec.CommonToolsLists.Count,
            }
        };
        return new OkObjectResult(result);
    }

    public async Task<IActionResult> ApplyAsync(CommonPromptDeploymentModel spec)
    {
        var operationId = Guid.NewGuid().ToString();

        // Map tools
        var commonPrompt = spec.Spec.CommonPrompts.Select(t =>
             ApiToRuntimeMapper.ToCommonPromptTool(t, operationId));

        if (commonPrompt == null || !commonPrompt.Any())
        {
            return new BadRequestObjectResult("No prompt provided in the deployment model.");
        }
        foreach (var prompt in commonPrompt)
            await _repository.UpsertCommonPromptAsync(prompt, operationId);

        await _extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();
        var result = new ExtendedAgentApply
        {
            Status = ExtendedAgentApplyStatus.Accepted,
            Message = "Common prompt deployment initiated",
            OperationId = "",
            Timestamp = DateTime.UtcNow,
            Details = new ExtendedAgentApplyDetails
            {
                ToolsCount = spec.Spec.CommonPrompts.Count,
            }
        };
        return new OkObjectResult(result);
    }

    public async Task<IActionResult> ApplyAsync(PluginConfigDeploymentModel pluginConfig)
    {
        var operationId = Guid.NewGuid().ToString();
        var pluginName = pluginConfig.Spec.PluginName;

        // Step 1: Lookup registered settings type
        if (!_pluginSettingsTypeRegistry.TryGetSettingsType(pluginName, out var settingsType))
        {
            return new BadRequestObjectResult($"No registered settings type found for plugin '{pluginName}'");
        }

        try
        {
            var tokenSource = _serviceProvider.GetService<ReloadableOptionsChangeTokenSource<IncidentManagementSettings>>();
            // Step 2: Apply configuration to the settings store
            if (tokenSource == null)
            {
                return new BadRequestObjectResult($"No registered settings type found for plugin '{pluginName}'");
            }
            PluginConfigApplier.Apply(pluginConfig, settingsType, _settingsStore, tokenSource);

            // Step 3: Convert to document and persist
            var document = ApiToRuntimeMapper.ToDocumentConfig(pluginConfig, operationId);
            await _repository.UpsertPluginConfigAsync(document);


            return new OkObjectResult(new { message = $"Plugin configuration applied for '{pluginName}'." });
        }
        catch (Exception ex)
        {

            return new ObjectResult($"Failed to apply configuration: {ex.Message}") { StatusCode = 500 };
        }
    }
}
