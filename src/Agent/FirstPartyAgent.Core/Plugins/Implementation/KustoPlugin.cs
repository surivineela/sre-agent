using System.ComponentModel;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins;
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

        var logMessage = $"[execute_kusto_query_on_cluster][{DateTime.UtcNow}] Invoked with cluster: {cluster}, database: {database}\nquery:\n{fullQuery.Substring(0, Math.Min(100, fullQuery.Length))}...";
        await kernel.LogInformation(logMessage, _logger, _teamsClient);
        KustoQueryResult result = null;
        try
        {
            result = await _kustoPluginClient.ExecuteClusterKustoQuery(cluster, database, fullQuery, NowOverride);
        }
        catch (Exception ex)
        {
            _logger.LogError($"An error occurred while executing Kusto Query: {ex.Message}");
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
            _logger.LogInformation($"Kusto query execution failed. Result: {result?.Result}, Message: {result?.Message}");
            return $"Kusto query execution failed.";
        }
    }
}
