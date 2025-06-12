using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Plugins.KustoPlugin;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Interface
{
    public interface IKustoPluginClient
    {
        public Task<KustoQueryResult> ExecuteClusterKustoQuery(string cluster, string database, string fullQuery, DateTime? NowOverride);
    }
}
