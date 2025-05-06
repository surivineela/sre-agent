// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using FirstPartyAgent.Constants;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Plugins.Definitions
{
    /// <summary>
    /// Using this approach because SK does not allow interfaces to be used as kernel functions
    /// https://github.com/microsoft/semantic-kernel/issues/10323
    /// </summary>
    public class KustoPluginDefinition(IKustoPlugin plugin)
    {
        private readonly IKustoPlugin _plugin = plugin;

        [KernelFunction(KernelFunctionNames.Kusto.ExecuteKustoQuery)]
        [Description("Executes a Kusto query on a regional cluster and returns the result in JSON format.")]
        public async Task<string> ExecuteKustoQuery(
            [Description("The region of the target Kusto cluster.")] string region,
            [Description("The Kusto query to execute.")] string query)
        {
            return await _plugin.ExecuteKustoQuery(region, query);
        }

        [KernelFunction(KernelFunctionNames.Kusto.ExecuteFunction)]
        [Description("Executes a user-defined Kusto function with named arguments on the regional Kusto cluster.")]
        public async Task<string> ExecuteFunction(
            [Description("The name of the user-defined function to execute.")] string functionName,
            [Description("The region of the Kusto cluster.")] string region,
            Dictionary<string, string> args)
        {
            return await _plugin.ExecuteFunctionAsync(functionName, region, args);
        }

        [KernelFunction(KernelFunctionNames.Kusto.ListKustoFunctions)]
        [Description("Lists all available Kusto functions in the specified region with metadata such as name and docstring.")]
        public async Task<string> ListFunctionsAsync(
            [Description("The region of the Kusto cluster to query for functions.")] string region)
        {
            var funcs = await _plugin.ListFunctionsAsync(region);
            return string.Join("\n", funcs.Select(f => $"- {f.Name}: {f.DocString}"));
        }

        [KernelFunction(KernelFunctionNames.Kusto.CreateAgentChatMessageForKustoQuery)]
        [Description("Creates a chat message with the role set to 'Tool' for a Kusto query or function execution. Includes a link to the Azure Data Explorer (ADX) and the query details.")]
        public Microsoft.Extensions.AI.ChatMessage CreateChatMessage(
            [Description("The Kusto query to execute.")] string query,
            [Description("The region of the target Kusto cluster or comlete cluster uri in the format https://{cluster}.kusto.windows.net")] string regionOrClusterUri,
            [Description("Database name against which to execute Kusto query. Must be non empty if using cluster URI")] string database = null,
            [Description("The name of the user-defined function to execute instead of the query.")] string functionName = null)
        {
            return _plugin.CreateChatMessage(query, regionOrClusterUri, database, functionName);
        }

        public Dictionary<string, Func<Task<string>>> GetRegisteredFunctionDelegates()
        {
            var map = new Dictionary<string, Func<Task<string>>>();
            var functions = _plugin.ListFunctionsAsync("eastus").Result.Where(c => c.Name == "CappsRevisions");

            foreach (var func in functions)
            {
                var name = func.Name;
                var args = new Dictionary<string, string>(); // could be populated dynamically in the future

                map[name] = async () => await _plugin.ExecuteFunctionAsync(name, "eastus", args);
            }

            return map;
        }
    }
}
