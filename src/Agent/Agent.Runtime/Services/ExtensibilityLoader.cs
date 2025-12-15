// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Framework.Skills;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

public class ExtensibilityLoader : IExtensibilityLoader
{
    private readonly ILogger<ExtensibilityLoader> _logger;

    private readonly IExtendedAgentRepository _extendedAgentRepository;

    public ExtensibilityLoader(
        ILogger<ExtensibilityLoader> logger,
        IExtendedAgentRepository extendedAgentRepository
        )
    {
        _logger = logger;
        _extendedAgentRepository = extendedAgentRepository;
    }

    public async Task<List<YamlCommonToolsDescriptor>> LoadExtendedCommonToolsListsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Migrating legacy cosmos db document before loading common tools lists...");
        await _extendedAgentRepository.MigrateCommonToolsListDocumentsAsync();

        _logger.LogInternalInformation("Starting custom agent files download...");
        List<YamlCommonToolsDescriptor> loadedCommonToolsLists = new List<YamlCommonToolsDescriptor>();
        try
        {
            var commonToolsLists = await _extendedAgentRepository.GetCommonToolsListsAsync(limit: 1000);
            foreach (var toolsList in commonToolsLists)
            {
                try
                {
                    loadedCommonToolsLists.Add(toolsList.ToRuntimeToolsList());
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to load common tools list from Cosmos: {ToolsListName}", toolsList.Name);
                }
            }

            return loadedCommonToolsLists;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "failed to load extended agents and tools");
            return new List<YamlCommonToolsDescriptor>();
        }
    }

    public async Task<List<YamlPromptDescriptor>> LoadExtendedCommonPromptsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Migrating legacy cosmos db document before loading common prompts...");
        await _extendedAgentRepository.MigrateCommonPromptDocumentsAsync();

        List<YamlPromptDescriptor> loadedCommonPrompts = new List<YamlPromptDescriptor>();

        try
        {
            var commonPrompts = await _extendedAgentRepository.GetCommonPromptsAsync(limit: 1000);
            foreach (var prompt in commonPrompts)
            {
                try
                {
                    loadedCommonPrompts.Add(prompt.ToYamlPromptDescriptor());
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to load common prompt from Cosmos: {PromptName}", prompt.Name);
                }
            }
            return loadedCommonPrompts;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "failed to load extended agents and tools");
            return new List<YamlPromptDescriptor>();
        }
    }

    public async Task<List<YamlToolDefinitionBase>> LoadExtendedToolsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Migrating legacy cosmos db document before loading extended tools...");
        await _extendedAgentRepository.MigrateToolDocumentsAsync();

        List<YamlToolDefinitionBase> loadedExtendedTools = new List<YamlToolDefinitionBase>();
        try
        {
            // load tools stored in Cosmos

            if (_extendedAgentRepository == null)
            {
                _logger.LogInternalWarning("ExtendedAgentRepository is not available. Cannot load extended tools on demand.");
                return loadedExtendedTools;
            }

            try
            {
                _logger.LogInternalInformation("Loading extended tools from Cosmos DB on demand...");

                // Load all extended tools
                var extendedTools = await _extendedAgentRepository.GetToolsAsync(limit: 1000);

                // Load new ones
                foreach (var extendedTool in extendedTools)
                {
                    try
                    {
                        loadedExtendedTools.Add(extendedTool.ToYamlToolDefinition());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalError(ex, "Failed to load extended tool {ToolName} from Cosmos DB on demand", extendedTool.Name);
                        // Continue loading other tools even if one fails
                    }
                }

                _logger.LogInternalInformation("Successfully loaded {Count} extended tools from Cosmos DB on demand", extendedTools.Count);
                return loadedExtendedTools;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to load extended tools from Cosmos DB on demand");
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "failed to load extended agents and tools");
            return loadedExtendedTools;
        }
    }

    public async Task<List<YamlAgentDescriptor>> LoadExtendedAgentsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Migrating legacy cosmos db document before loading extended agents...");
        await _extendedAgentRepository.MigrateAgentDocumentsAsync();

        _logger.LogInternalInformation("Starting custom agent files download...");
        List<YamlAgentDescriptor> loadedExtendedAgents = new List<YamlAgentDescriptor>();
        try
        {
            var extendedAgents = await _extendedAgentRepository.GetAgentsAsync(limit: 1000);
            foreach (var extendedAgent in extendedAgents)
            {
                try
                {
                    loadedExtendedAgents.Add(extendedAgent.ToYamlAgentDescriptor());
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to load extended agent from Cosmos: {AgentName}", extendedAgent.Name);
                }
            }
            return loadedExtendedAgents;
            // load tools stored in Cosmos
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "failed to load extended agents and tools");
            return new List<YamlAgentDescriptor>();
        }
    }

    public async Task<List<SkillSpec>> LoadExtendedSkillsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Migrating legacy cosmos db document before loading extended skills...");
        await _extendedAgentRepository.MigrateSkillDocumentsAsync();

        _logger.LogInternalInformation("Starting custom skill files download...");
        List<SkillSpec> loadedExtendedSkills = [];
        try
        {
            var extendedSkills = await _extendedAgentRepository.GetSkillsAsync(limit: 1000);
            foreach (var extendedSkill in extendedSkills)
            {
                try
                {
                    var concreteSkill = extendedSkill.ToRuntimeModel();

                    loadedExtendedSkills.Add(concreteSkill);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to load extended skill from Cosmos: {SkillName}", extendedSkill.Id);
                }
            }
            return loadedExtendedSkills;
            // load tools stored in Cosmos
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "failed to load extended skills");
            return [];
        }
    }
}
