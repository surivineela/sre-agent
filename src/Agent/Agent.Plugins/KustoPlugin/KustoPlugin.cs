using System.ComponentModel;
using Agent.Framework;
using Agent.Logging;
using Agent.Plugins.Interface;
using Agent.Plugins.TeamsPlugin;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
namespace Agent.Plugins.KustoPlugin;

// [Export]
[AgentToolPlugin(IsFirstPartyOnly = true)]
public class KustoPlugin
{
    private readonly ILogger<KustoPlugin> _logger;
    private readonly ITeamsClient _teamsClient;
    private readonly IKustoPluginClient _kustoPluginClient;

    public KustoPlugin(ILogger<KustoPlugin> logger, ITeamsClient teamsClient, IKustoPluginClient kustoPluginClient)
    {
        _logger = logger;
        _teamsClient = teamsClient;
        _logger = logger;
        _kustoPluginClient = kustoPluginClient;
    }

    [KernelFunction("execute_kusto_query_on_cluster")]
    [Description("Executes a fully qualified Kusto query on a specific cluster and database, returning the result in JSON format.")]
    public async Task<string> ExecuteClusterKustoQuery(
            [Description("The short name of the target Kusto cluster (without URL schema or suffix).")] string cluster,
            [Description("The name of the target Kusto database.")] string database,
            [Description("The full Kusto query to execute.")] string fullQuery,
            DateTime? NowOverride,
            Kernel kernel
            )
    {
        cluster = cluster.Replace(".kusto.windows.net", "");
        cluster = cluster.Replace("https://", "");

        
        KustoQueryResult result = null;
        try
        {
            result = await _kustoPluginClient.ExecuteClusterKustoQuery(cluster, database, fullQuery, NowOverride);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"An error occurred while executing Kusto Query: {ex.Message}");
        }

        if (result != null && result.Result != null)
        {
            if (result.RowCount == 0 && !result.Result.StartsWith("An error occurred while executing Kusto Query"))
            {
                return "ZERO_ROWS_RETURNED";
            }
            return result.Result;
        }
        else
        {
            _logger.LogInternalInformation($"Kusto query execution failed. Result: {result?.Result}, Message: {result?.Message}");
            return $"Kusto query execution failed.";
        }
    }
}
