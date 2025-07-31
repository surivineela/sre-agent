using Agent.Framework;
using Agent.Framework.Reasoning.Models;

namespace Agent.Runtime.Interfaces;

public interface IExtendedAgentService
{
    Task<PaginatedList<YamlAgentDescriptor>> GetAgentsAsync(int pageIndex, int limit, string? search);

    Task<PaginatedList<YamlToolDefinitionBase>> GetToolsAsync(int pageIndex, int limit, string? search);

    Task<PaginatedList<DataConnectorDefinitionBase>> GetConnectorsAsync(int pageIndex, int limit, string? search);

    Task RefreshAgentAndToolsRegisterationsAsync();
}
