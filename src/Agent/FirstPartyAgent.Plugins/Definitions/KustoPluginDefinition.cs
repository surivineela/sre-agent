using System.ComponentModel;
using FirstPartyAgent.Constants;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Plugins
{
    /// <summary>
    /// Using this approach because SK does not allow interfaces to be used as kernel functions
    /// https://github.com/microsoft/semantic-kernel/issues/10323
    /// </summary>
    /// <param name="plugin"></param>
    public class KustoPluginDefinition(IKustoPlugin plugin)
    {
        private readonly IKustoPlugin _plugin = plugin;

        [KernelFunction(KernelFunctionNames.Kusto.ExecuteKustoQuery)]
        public async Task<string> ExecuteKustoQuery(
            [Description("The region of the target kusto")] string region,
            [Description("The query to execute")] string query)
        {
            return await _plugin.ExecuteKustoQuery(region, query);
        }
    }
}
