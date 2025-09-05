// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
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
    private readonly IYamlValidatorFactory _validatorFactory;
    private readonly IAgentYamlTranslatorFactory _translatorFactory;
    private readonly IExtendedAgentRepository _repository;
    private readonly IExtendedAgentService _extendedAgentService;
    private readonly IPluginSettingsTypeRegistry _pluginSettingsTypeRegistry;
    private readonly IReloadableSettingsStore _settingsStore;
    private readonly IServiceProvider _serviceProvider;

    public ResourceDeploymentService(
        IYamlValidatorFactory validatorFactory,
        IAgentYamlTranslatorFactory translatorFactory,
        IExtendedAgentRepository repository,
        IExtendedAgentService extendedAgentService,
        IReloadableSettingsStore settingsStore,
        IPluginSettingsTypeRegistry pluginSettingsTypeRegistry,
        IServiceProvider serviceProvider)
    {
        _validatorFactory = validatorFactory;
        _translatorFactory = translatorFactory;
        _repository = repository;
        _extendedAgentService = extendedAgentService;
        _pluginSettingsTypeRegistry = pluginSettingsTypeRegistry;
        _settingsStore = settingsStore;
        _serviceProvider = serviceProvider;

    }

    public async Task<IActionResult> ApplyAsync(AgentDeploymentModel spec)
    {
        var operationId = Guid.NewGuid().ToString();

        // Map agent
        var agentDoc = new AgentDocumentModel(
            Name: spec.Spec.Name,
            Id: spec.Spec.Name,
            Instructions: spec.Spec.Instructions,
            HandoffDescription: spec.Spec.HandoffDescription,
            Handoffs: spec.Spec.Handoffs,
            Tools: spec.Spec.Tools,
            Connectors: spec.Spec.Connectors,
            AllowParallelToolCalls: spec.Spec.AllowParallelToolCalls,
            AgentsAsTools: spec.Spec.AgentsAsTools,
            MaxReflectionCount: spec.Spec.MaxReflectionCount,
            CriticPromptPath: spec.Spec.CriticPromptPath,
            CriticOnHandOff: spec.Spec.CriticOnHandOff,
            CustomReflectionNote: spec.Spec.CustomReflectionNote,
            CommonPrompts: spec.Spec.CommonPrompts,
            CommonTools: spec.Spec.CommonTools,
            DisableDocumentRetrieval: spec.Spec.DisableDocumentRetrieval,
            EnableHandoffPromptOverride: spec.Spec.EnableHandoffPromptOverride,
            HandoffPromptOverride: spec.Spec.HandoffPromptOverride,
            UserPromptOverride: spec.Spec.UserPromptOverride,
            InstructionsOverride: spec.Spec.InstructionsOverride,
            Temperature: spec.Spec.Temperature,
            // Workflow agent properties
            AgentType: spec.Spec.AgentType,
            ParameterExtractionAgent: spec.Spec.ParameterExtractionAgent,
            OrchestrationStartAgents: spec.Spec.OrchestrationStartAgents,
            ResultSummarizationPrompt: spec.Spec.ResultSummarizationPrompt,
            NextAgentMappings: spec.Spec.NextAgentMappings,
            OutputType: spec.Spec.OutputType,
            Metadata: spec.Spec.Metadata,
            OperationId: operationId
        );

        // Persist agent to Cosmos (tools and connectors are already referenced by name in the agent document)
        await _repository.UpdateAgentAsync(agentDoc, operationId);
        await _extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();
        var result = new ExtendedAgentApply
        {
            Status = ExtendedAgentApplyStatus.Accepted,
            Message = "Agent and tools deployment initiated",
            OperationId = "",
            Timestamp = DateTime.UtcNow,
            Details = new ExtendedAgentApplyDetails
            {
                AgentName = spec.Spec.Name,
                ToolsCount = spec.Spec.Tools?.Count ?? 0,
                ConnectorsCount = spec.Spec.Connectors?.Count ?? 0
            }
        };

        return new OkObjectResult(result);
    }

    public async Task<IActionResult> ApplyAsync(ConnectorsDeploymentModel spec)
    {
        var operationId = Guid.NewGuid().ToString();

        var connectorDocs = spec.Spec.Connectors.Select(c =>
                ApiToRuntimeMapper.ToDocumentConnector(c, operationId));


        foreach (var connector in connectorDocs)
            await _repository.UpdateConnectorAsync(connector, operationId);

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
            await _repository.UpdateToolAsync(tool, operationId);

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
            await _repository.UpdateCommonToolsListAsync(toolList, operationId);

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
            await _repository.UpdateCommonPromptAsync(prompt, operationId);

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
            await _repository.UpdatePluginConfigAsync(document);


            return new OkObjectResult(new { message = $"Plugin configuration applied for '{pluginName}'." });
        }
        catch (Exception ex)
        {

            return new ObjectResult($"Failed to apply configuration: {ex.Message}") { StatusCode = 500 };
        }
    }
}
