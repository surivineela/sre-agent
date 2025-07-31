// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Runtime.Interfaces;
using Agent.Framework.Reasoning.Models;
using Agent.Web.Services;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

public class ExtendedAgentService : IExtendedAgentService
{
    private readonly ILogger<ExtendedAgentService> _logger;
    private readonly IAgentFactory<AgentContext> _agentFactory;
    private readonly IToolFactory<AgentContext> _toolFactory;
    private readonly IExtendedAgentRepository _extendedAgentRepository;

    public ExtendedAgentService(
        ILogger<ExtendedAgentService> logger,
        IAgentFactory<AgentContext> agentFactory,
        IToolFactory<AgentContext> toolFactory,
        IExtendedAgentRepository extendedAgentRepository

        )
    {
        _logger = logger;
        _agentFactory = agentFactory;
        _toolFactory = toolFactory;
        _extendedAgentRepository = extendedAgentRepository;
    }

    public async Task<PaginatedList<YamlAgentDescriptor>> GetAgentsAsync(int pageIndex, int limit, string? search)
    {
        var all = await _extendedAgentRepository.GetAgentsAsync();

        var filtered = all
            .Where(a => string.IsNullOrEmpty(search) || a.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

        var mapped = filtered
            .Select(c => DocumentToRuntimeMapper.ToRuntimeAgent(c));

        return PaginatedList<YamlAgentDescriptor>.Create(mapped, pageIndex, limit);
    }

    public async Task<PaginatedList<DataConnectorDefinitionBase>> GetConnectorsAsync(int pageIndex, int limit, string? search)
    {
        try
        {
            var all = await _extendedAgentRepository.GetConnectorsAsync(); // remove limit from repository call
            var filtered = all
                .Where(c => string.IsNullOrEmpty(search) || c.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            var mapped = filtered
                .Select(c => DocumentToRuntimeMapper.ToRuntimeConnector(c));

            return PaginatedList<DataConnectorDefinitionBase>.Create(mapped, pageIndex, limit);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to retrieve connectors with search '{Search}' and limit {Limit}", search ?? "none", limit);
            throw;
        }
    }

    public async Task<PaginatedList<YamlToolDefinitionBase>> GetToolsAsync(int pageIndex, int limit, string? search)
    {
        try
        {
            var all = await _extendedAgentRepository.GetToolsAsync(); // remove limit from repository call
            var filtered = all
                .Where(t => string.IsNullOrEmpty(search) || t.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            var mapped = filtered
                .Select(t => DocumentToRuntimeMapper.ToRuntimeTool(t));

            return PaginatedList<YamlToolDefinitionBase>.Create(mapped, pageIndex, limit);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to retrieve tools with search '{Search}' and limit {Limit}", search ?? "none", limit);
            throw;
        }
    }

    public async Task RefreshAgentAndToolsRegisterationsAsync()
    {
        _logger.LogInternalInformation("Starting custom agent files download...");

        try
        {
            var extendedAgents = await _extendedAgentRepository.GetAgentsAsync(limit: 1000);
            foreach (var extendedAgent in extendedAgents)
            {
                try
                {
                    var concreteAgent = DocumentToRuntimeMapper.ToRuntimeAgent(extendedAgent);
                    _agentFactory.LoadAgentFromDescriptor(concreteAgent, true);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to load extended agent from Cosmos: {AgentName}", extendedAgent.Name);
                }
            }
            _agentFactory.UpdateHandoffs();
            // load tools stored in Cosmos
            await _toolFactory.LoadExtendedToolsFromCosmosOnDemandAsync();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "failed to load extended agents and tools");
            return;
        }
    }
}
