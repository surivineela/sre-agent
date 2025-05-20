using System.ComponentModel;
using FirstPartyAgent.Core.Clients;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins;
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
            string fullQuery,
            DateTime? NowOverride)
    {
        cluster = cluster.Replace(".kusto.windows.net", "");
        cluster = cluster.Replace("https://", "");
        var reader = await _kustoClient.PerformQueryAsync($"https://{cluster}.kusto.windows.net", database, fullQuery);
        return new KustoQueryResult(reader, fullQuery);
    }
}
