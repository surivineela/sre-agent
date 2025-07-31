using Agent.Core.Interfaces;
using Agent.Data.DataModels;

namespace Agent.Evals;

internal class InMemoryExtendedAgentRepository : IExtendedAgentRepository
{
    List<AgentDocumentModel> agents = new();

    List<ToolDocumentModel> tools = new();

    List<ConnectorDocumentModel> connectors = new();
    public Task<AgentDocumentModel> CreateAgentAsync(AgentDocumentModel agent, string operationId)
    {
        agents.Add(agent);
        return Task.FromResult(agent);
    }

    public Task<ConnectorDocumentModel> CreateConnectorAsync(ConnectorDocumentModel connector, string operationId)
    {
        connectors.Add(connector);  
        return Task.FromResult(connector);
    }

    public Task<ToolDocumentModel> CreateToolAsync(ToolDocumentModel tool, string operationId)
    {
        tools.Add(tool);    
        return Task.FromResult(tool);
    }

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

        return Task.FromResult(new PaginatedList<AgentDocumentModel>(items, totalCount,0, limit));
    }

    public Task<ConnectorDocumentModel?> GetConnectorByNameAsync(string name)
    {
        var connector = connectors.FirstOrDefault(c => c.Name == name);
        return Task.FromResult(connector);
    }

    public Task<PaginatedList<ConnectorDocumentModel>> GetConnectorsAsync(int limit = 50, string? search = null)
    {
        throw new NotImplementedException();
    }

    public Task<ToolDocumentModel?> GetToolByNameAsync(string name)
    {
        throw new NotImplementedException();
    }

    public Task<PaginatedList<ToolDocumentModel>> GetToolsAsync(int limit = 50, string? search = null)
    {
        throw new NotImplementedException();
    }

    public Task<AgentDocumentModel> UpdateAgentAsync(AgentDocumentModel agent, string operationId)
    {
        throw new NotImplementedException();
    }

    public Task<ConnectorDocumentModel> UpdateConnectorAsync(ConnectorDocumentModel connector, string operationId)
    {
        throw new NotImplementedException();
    }

    public Task<PlugInConfigDocumentModel> UpdatePluginConfigAsync(PlugInConfigDocumentModel config)
    {
        throw new NotImplementedException();
    }

    public Task<ToolDocumentModel> UpdateToolAsync(ToolDocumentModel tool, string operationId)
    {
        throw new NotImplementedException();
    }
}
