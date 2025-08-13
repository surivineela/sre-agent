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
            Name: spec.Spec.Agent.Name,
            Id: spec.Spec.Agent.Name,
            Instructions: spec.Spec.Agent.Instructions,
            HandoffDescription: spec.Spec.Agent.HandoffDescription,
            Handoffs: spec.Spec.Agent.Handoffs,
            Tools: spec.Spec.Agent.Tools,
            Connectors: spec.Spec.Agent.Connectors,
            AllowParallelToolCalls: spec.Spec.Agent.AllowParallelToolCalls,
            AgentsAsTools: spec.Spec.Agent.AgentsAsTools,
            MaxReflectionCount: spec.Spec.Agent.MaxReflectionCount,
            CriticPromptPath: spec.Spec.Agent.CriticPromptPath,
            CriticOnHandOff: spec.Spec.Agent.CriticOnHandOff,
            CustomReflectionNote: spec.Spec.Agent.CustomReflectionNote,
            CommonPrompts: spec.Spec.Agent.CommonPrompts,
            CommonTools: spec.Spec.Agent.CommonTools,
            DisableDocumentRetrieval: spec.Spec.Agent.DisableDocumentRetrieval,
            EnableHandoffPromptOverride: spec.Spec.Agent.EnableHandoffPromptOverride,
            HandoffPromptOverride: spec.Spec.Agent.HandoffPromptOverride,
            UserPromptOverride: spec.Spec.Agent.UserPromptOverride,
            InstructionsOverride: spec.Spec.Agent.InstructionsOverride,
            Temperature: spec.Spec.Agent.Temperature,
            // Workflow agent properties
            AgentType: spec.Spec.Agent.AgentType,
            ParameterExtractionAgent: spec.Spec.Agent.ParameterExtractionAgent,
            OrchestrationStartAgents: spec.Spec.Agent.OrchestrationStartAgents,
            ResultSummarizationPrompt: spec.Spec.Agent.ResultSummarizationPrompt,
            NextAgentMappings: spec.Spec.Agent.NextAgentMappings,
            OutputType: spec.Spec.Agent.OutputType,
            Metadata: spec.Spec.Agent.Metadata,
            OperationId: operationId
        );

        // Map tools
        var toolDocs = spec.Spec.Tools.Select(t =>
            ApiToRuntimeMapper.ToDocumentTool(t, operationId)).ToList();

        // Map connectors
        var connectorDocs = spec.Spec.Connectors.Select(c =>
            ApiToRuntimeMapper.ToDocumentConnector(c, operationId)).ToList();

        // Persist agent + tools + connectors to Cosmos
        await _repository.UpdateAgentAsync(agentDoc, operationId);
        foreach (var tool in toolDocs)
            await _repository.UpdateToolAsync(tool, operationId);

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
                AgentName = spec.Spec.Agent?.Name,
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
