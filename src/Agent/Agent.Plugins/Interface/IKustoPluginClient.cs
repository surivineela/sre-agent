using Agent.Plugins.KustoPlugin;

namespace Agent.Plugins.Interface
{
    public interface IKustoPluginClient
    {
        public Task<KustoQueryResult> ExecuteClusterKustoQuery(string cluster, string database, string fullQuery);
    }
}
