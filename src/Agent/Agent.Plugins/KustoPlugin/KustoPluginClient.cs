using Agent.Plugins.Interface;
using Agent.Plugins.KustoPlugin;

namespace Agent.Plugins.Kusto;
public class KustoPluginClient: IKustoPluginClient
{
    private readonly KustoClient _kustoClient;

    public KustoPluginClient(KustoClient kustoClient)
    {
        _kustoClient = kustoClient;
    }

    public async Task<KustoQueryResult> ExecuteClusterKustoQuery(
            string cluster,
            string database,
            string fullQuery)
    {
        cluster = cluster.Replace(".kusto.windows.net", "");
        cluster = cluster.Replace("https://", "");
        var reader = await _kustoClient.PerformQueryAsync($"https://{cluster}.kusto.windows.net", database, fullQuery);
        return new KustoQueryResult(reader, fullQuery);
    }
}
