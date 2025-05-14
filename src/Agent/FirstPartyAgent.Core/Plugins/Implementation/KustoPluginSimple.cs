using System.ComponentModel;
using FirstPartyAgent.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins;
public class KustoPluginSimple
{
    private readonly ILogger<KustoPluginSimple> _logger;
    private readonly IKustoPlugin _kustoPlugin;

    public KustoPluginSimple(ILogger<KustoPluginSimple> logger, IKustoPlugin kustoPlugin)
    {
        _logger = logger;
        _kustoPlugin = kustoPlugin;
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
        var result = await _kustoPlugin.ExecuteClusterKustoQuery(cluster, database, fullQuery, NowOverride, kernel);
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
