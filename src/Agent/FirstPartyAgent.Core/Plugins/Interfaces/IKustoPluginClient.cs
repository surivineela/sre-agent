using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirstPartyAgent.Plugins;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Interfaces;
public interface IKustoPluginClient
{
    public Task<KustoQueryResult> ExecuteClusterKustoQuery(string cluster, string database, string fullQuery, DateTime? NowOverride);
}
