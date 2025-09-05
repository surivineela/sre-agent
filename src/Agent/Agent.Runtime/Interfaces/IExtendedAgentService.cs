using Agent.Framework;
using Agent.Framework.Reasoning.Models;

namespace Agent.Runtime.Interfaces;

public interface IExtendedAgentService
{
    Task<PaginatedList<YamlAgentDescriptor>> GetAgentsAsync(int pageIndex, int limit, string? search);

    Task<PaginatedList<YamlToolDefinitionBase>> GetToolsAsync(int pageIndex, int limit, string? search);

    Task<bool> DeleteAgentAsync(string agentName);

    Task RefreshAgentAndToolsRegisterationsAsync();

    Task<(bool deleted, List<string> dependentAgents)> DeleteToolAsync(string toolName);
}
