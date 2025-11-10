// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using Agent.Framework.Skills;

namespace Agent.Runtime.Interfaces;

public interface IExtendedAgentService
{
    Task<PaginatedList<YamlAgentDescriptor>> GetAgentsAsync(int pageIndex, int limit, string? search);

    Task<YamlAgentDescriptor?> GetAgentByNameAsync(string agentName);

    Task<PaginatedList<YamlToolDefinitionBase>> GetToolsAsync(int pageIndex, int limit, string? search);

    Task<bool> DeleteAgentAsync(string agentName);

    Task RefreshAgentAndToolsRegisterationsAsync();

    Task<(bool deleted, List<string> dependentAgents)> DeleteToolAsync(string toolName);

    List<string> ValidateYamlStructure(Dictionary<string, object> rootDocument);

    Task<PaginatedList<SkillSpec>> GetSkillsAsync(int pageIndex, int limit, string? search);

    Task<SkillSpec?> GetSkillByNameAsync(string skillName);

    Task<bool> DeleteSkillAsync(string skillName);
}
