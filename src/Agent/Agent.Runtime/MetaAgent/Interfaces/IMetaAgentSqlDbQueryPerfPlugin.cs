using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.RdpInvestigatorAgent;
using Agent.Runtime.SubAgents.SqlDbQueryPerfAgent;

namespace Agent.Runtime.MetaAgent;

/// <summary>
/// Interface for SqlDbQueryPerfPlugin
/// </summary>
public interface IMetaAgentSqlDbQueryPerfPlugin
{
    /// <summary>
    /// Gets or sets the thread context
    /// </summary>
    public ThreadContext? Context { get; set; }

    /// <summary>
    /// Lists Azure SQL DB Query Perf investigator workflows
    /// </summary>
    /// <returns>List of Az Sql Db investigator workflows</returns>
    Task<IReadOnlyList<WorkflowMetadata<SqlDbQueryPerfAgentInput>>> ListAzureSqlDbQueryPerfInvestigatorAgentWorkflows();

    /// <summary>
    /// Starts the Azure SQL DB Query Investigator Agent
    /// </summary>
    /// <param name="input">The input data for the agent</param>
    /// <returns>Result of starting the agent</returns>
    Task<string> StartAzureSqlDbQueryPerfInvestigatorAgent(string sqlDbResourceId);
}
