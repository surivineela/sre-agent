// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Data;
using Agent.Framework;
using Agent.Framework.Interfaces;
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
        _logger.LogInternalInformation("Starting custom agent files download...");
        List<YamlCommonToolsDescriptor> loadedCommonToolsLists = new List<YamlCommonToolsDescriptor>();
        try
        {
            var commonToolsLists = await _extendedAgentRepository.GetCommonToolsListsAsync(limit: 1000);
            foreach (var toolsList in commonToolsLists)
            {
                try
                {
                    var concreteToolsList = DocumentToRuntimeMapper.ToRuntimeToolsList(toolsList);
                    loadedCommonToolsLists.Add(concreteToolsList);
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
        List<YamlPromptDescriptor> loadedCommonPrompts = new List<YamlPromptDescriptor>();

        try
        {
            var commonPrompts = await _extendedAgentRepository.GetCommonPromptsAsync(limit: 1000);
            foreach (var prompt in commonPrompts)
            {
                try
                {
                    var concretePrompt = DocumentToRuntimeMapper.ToRuntimePrompt(prompt);
                    loadedCommonPrompts.Add(concretePrompt);
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
                        var concretetool = DocumentToRuntimeMapper.ToRuntimeTool(extendedTool);

                        loadedExtendedTools.Add(concretetool);
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
        _logger.LogInternalInformation("Starting custom agent files download...");
        List<YamlAgentDescriptor> loadedExtendedAgents = new List<YamlAgentDescriptor>();
        try
        {
            var extendedAgents = await _extendedAgentRepository.GetAgentsAsync(limit: 1000);
            foreach (var extendedAgent in extendedAgents)
            {
                try
                {
                    var concreteAgent = DocumentToRuntimeMapper.ToRuntimeAgent(extendedAgent);
                    loadedExtendedAgents.Add(concreteAgent);
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
}
