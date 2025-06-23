using System.ComponentModel;
using Agent.Logging;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;
namespace Agent.Plugins.KustoPlugin;


[AgentToolPlugin(IsFirstPartyOnly = true)]
public class KustoPlugin
{
    private readonly ILogger<KustoPlugin> _logger;
    private readonly IKustoPluginClient _kustoPluginClient;

    public KustoPlugin(ILogger<KustoPlugin> logger, IKustoPluginClient kustoPluginClient)
    {
        _logger = logger;
        _kustoPluginClient = kustoPluginClient;
    }

    [Description("Executes a fully qualified Kusto query on a specific cluster and database, returning the result in JSON format.")]
    public async Task<string> ExecuteClusterKustoQuery(
            [Description("The short name of the target Kusto cluster (without URL schema or suffix).")] string cluster,
            [Description("The name of the target Kusto database.")] string database,
            [Description("The full Kusto query to execute.")] string fullQuery,
            DateTime? NowOverride
            )
    {
        cluster = cluster.Replace(".kusto.windows.net", "");
        cluster = cluster.Replace("https://", "");

        var logMessage = $"[execute_kusto_query_on_cluster][{DateTime.UtcNow}] Invoked with cluster: {cluster}, database: {database}\nquery:\n{fullQuery.Substring(0, Math.Min(100, fullQuery.Length))}...";
        _logger.LogInternalInformation(logMessage);
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
