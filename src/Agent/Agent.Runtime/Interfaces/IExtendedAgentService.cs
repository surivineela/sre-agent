// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;

namespace Agent.Runtime.Interfaces;

public interface IExtendedAgentService
{
    Task<PaginatedList<YamlAgentDescriptor>> GetAgentsAsync(int pageIndex, int limit, string? search);

    Task<PaginatedList<YamlToolDefinitionBase>> GetToolsAsync(int pageIndex, int limit, string? search);

    Task<bool> DeleteAgentAsync(string agentName);

    Task RefreshAgentAndToolsRegisterationsAsync();

    Task<(bool deleted, List<string> dependentAgents)> DeleteToolAsync(string toolName);

    List<string> ValidateYamlStructure(Dictionary<string, object> rootDocument);
}
