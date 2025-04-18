using System.ComponentModel;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.SqlDbQueryPerfAgent;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;

namespace Agent.Runtime.MetaAgent;
public class SqlDbQueryPerfPlugin: IMetaAgentSqlDbQueryPerfPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly SqlDbQueryPerfAgentFactory _sqlDbQueryPerfAgentFactory;

    public ThreadContext? Context { get; set; }

    public SqlDbQueryPerfPlugin(DurableTaskClient durableTaskClient, SqlDbQueryPerfAgentFactory sqlDbQueryPerfAgentFactory)
    {
        _durableTaskClient = durableTaskClient;
        _sqlDbQueryPerfAgentFactory= sqlDbQueryPerfAgentFactory;
    }

    [KernelFunction("list_az_sqldb_query_perf_investigate_workflows")]
    [Description("List the information of started Azure Sql Db query performance investigation workflows")]
    public async Task<IReadOnlyList<WorkflowMetadata<SqlDbQueryPerfAgentInput>>> ListAzureSqlDbQueryPerfInvestigatorAgentWorkflows()
    {
        var list = new List<WorkflowMetadata<SqlDbQueryPerfAgentInput>>();

        try
        {
            await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
            {
                try
                {
                    if (instance.SerializedInput?.Contains("sqldbquery", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        var input = _sqlDbQueryPerfAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
                        list.Add(new WorkflowMetadata<SqlDbQueryPerfAgentInput>(
                            WorkflowInstanceId: instance.InstanceId,
                            Input: input));
                    }
                }
                catch
                {
                    // Ignore deserialization errors
                }
            }
        }
        catch
        {
            // Ignore errors while fetching instances
        }

        return list;
    }

    [KernelFunction("start_az_sqldb_query_perf_investigate_workflow")]
    [Description("Start the workflow to investigate query performance issues against an Azure SQL DB resource.")]
    public async Task<string> StartAzureSqlDbQueryPerfInvestigatorAgent(
        [Description("Arm resource id for the Azure sql db resource to investigate query performance for")] string sqlDbResourceId)
    {
        if (Context == null)
        {
            throw new InvalidOperationException("Thread context is not set. Please set the context before starting the workflow.");
        }

        if (!string.IsNullOrWhiteSpace(sqlDbResourceId) && sqlDbResourceId.IndexOf("/providers/Microsoft.Sql/servers/", StringComparison.OrdinalIgnoreCase) > -1 && sqlDbResourceId.IndexOf("/databases/", StringComparison.OrdinalIgnoreCase) > -1)
        {
            var instanceId = await _sqlDbQueryPerfAgentFactory.StartOrchestration(sqlDbResourceId, Context);
            return $"A workflow has been started to investigate query performance issues against azure sql database: {instanceId}";
        }
        else
        {
            throw new InvalidOperationException("Resource id must be an Azurre SQL DB resource of the form /subscriptions/SUBSCRIPTION_ID_GUID/resourceGroups/RESOURCE_GROUP_NAME/providers/Microsoft.Sql/servers/SERVER_NAME/databases/DB_NAME");
        }
    }
}
