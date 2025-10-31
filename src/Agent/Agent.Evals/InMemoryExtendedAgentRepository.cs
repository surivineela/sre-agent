using Agent.Core.Interfaces;
using Agent.Data.DataModels;

namespace Agent.Evals;

internal class InMemoryExtendedAgentRepository : IExtendedAgentRepository
{
    List<AgentDocumentModel> agents = new();

    List<ToolDocumentModel> tools = new();

    List<ConnectorDocumentModel> connectors = new();

    List<CommonPromptDocumentModel> commonPrompts = new();
    List<CommonToolsListDocumentModel> commonToolsLists = new();

    public Task<bool> DeleteAgentAsync(string name)
    {
        agents.RemoveAll(a => a.Name == name);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteConnectorAsync(string name)
    {
        connectors.RemoveAll(c => c.Name == name);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteToolAsync(string name)
    {
        tools.RemoveAll(t => t.Name == name);
        return Task.FromResult(true);
    }

    public Task<AgentDocumentModel?> GetAgentByNameAsync(string name)
    {
        var agent = agents.FirstOrDefault(a => a.Name == name);
        return Task.FromResult(agent);
    }

    public Task<PaginatedList<AgentDocumentModel>> GetAgentsAsync(int limit = 50, string? search = null)
    {
        var filteredAgents = agents.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filteredAgents = filteredAgents.Where(a => a.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = filteredAgents.Count();
        var items = filteredAgents.Take(limit).ToList();

        return Task.FromResult(new PaginatedList<AgentDocumentModel>(items, totalCount, 0, limit));
    }

    public Task<PaginatedList<CommonPromptDocumentModel>> GetCommonPromptsAsync(int limit = 50, string? search = null)
    {
        var filteredPrompts = commonPrompts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filteredPrompts = filteredPrompts.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = filteredPrompts.Count();
        var items = filteredPrompts.Take(limit).ToList();

        return Task.FromResult(new PaginatedList<CommonPromptDocumentModel>(items, totalCount, 0, limit));
    }

    public Task<PaginatedList<CommonToolsListDocumentModel>> GetCommonToolsListsAsync(int limit = 50, string? search = null)
    {
        var filteredToolsLists = commonToolsLists.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filteredToolsLists = filteredToolsLists.Where(t => t.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = filteredToolsLists.Count();
        var items = filteredToolsLists.Take(limit).ToList();

        return Task.FromResult(new PaginatedList<CommonToolsListDocumentModel>(items, totalCount, 0, limit));
    }

    public Task<ConnectorDocumentModel?> GetConnectorByNameAsync(string name)
    {
        var connector = connectors.FirstOrDefault(c => c.Name == name);
        return Task.FromResult(connector);
    }

    public Task<PaginatedList<ConnectorDocumentModel>> GetConnectorsAsync(int limit = 50, string? search = null)
    {
        var filteredConnectors = connectors.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filteredConnectors = filteredConnectors.Where(c => c.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = filteredConnectors.Count();
        var items = filteredConnectors.Take(limit).ToList();

        return Task.FromResult(new PaginatedList<ConnectorDocumentModel>(items, totalCount, 0, limit));
    }

    public Task<ToolDocumentModel?> GetToolByNameAsync(string name)
    {
        return Task.FromResult(tools.FirstOrDefault(t => t.Name == name));
    }

    public Task<PaginatedList<ToolDocumentModel>> GetToolsAsync(int limit = 50, string? search = null)
    {
        var filteredTools = tools.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filteredTools = filteredTools.Where(t => t.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = filteredTools.Count();
        var items = filteredTools.Take(limit).ToList();

        return Task.FromResult(new PaginatedList<ToolDocumentModel>(items, totalCount, 0, limit));
    }

    public Task<AgentDocumentModel> UpsertAgentAsync(AgentDocumentModel agent, string operationId)
    {
        agents.RemoveAll(a => a.Name == agent.Name);
        agents.Add(agent);
        return Task.FromResult(agent);
    }

    public Task<CommonPromptDocumentModel> UpsertCommonPromptAsync(CommonPromptDocumentModel prompt, string operationId)
    {
        commonPrompts.RemoveAll(p => p.Name == prompt.Name);
        commonPrompts.Add(prompt);
        return Task.FromResult(prompt);
    }

    public Task<CommonToolsListDocumentModel> UpsertCommonToolsListAsync(CommonToolsListDocumentModel toolsList, string operationId)
    {
        commonToolsLists.RemoveAll(t => t.Name == toolsList.Name);
        commonToolsLists.Add(toolsList);
        return Task.FromResult(toolsList);
    }

    public Task<ConnectorDocumentModel> UpsertConnectorAsync(ConnectorDocumentModel connector, string operationId)
    {
        throw new NotImplementedException();
    }

    public Task<PlugInConfigDocumentModel> UpsertPluginConfigAsync(PlugInConfigDocumentModel config)
    {
        throw new NotImplementedException();
    }

    public Task<ToolDocumentModel> UpsertToolAsync(ToolDocumentModel tool, string operationId)
    {
        throw new NotImplementedException();
    }
}
