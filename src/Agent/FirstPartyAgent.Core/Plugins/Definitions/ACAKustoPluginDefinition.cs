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
    public class ACAKustoPluginDefinition(IACAKustoPlugin plugin)
    {
        private readonly IACAKustoPlugin _plugin = plugin;

        [KernelFunction(KernelFunctionNames.Kusto.ExecuteKustoQuery)]
        [Description("Executes a Kusto query on a regional cluster and returns the result in JSON format.")]
        public async Task<string> ExecuteKustoQuery(
            [Description("The region of the target Kusto cluster.")] string region,
            [Description("The Kusto query to execute.")] string query,
            [Description("Optional group name")] string groupName)
        {
            var result = await _plugin.ExecuteKustoQuery(region, query, groupName);
            return result.Result;
        }

        [KernelFunction(KernelFunctionNames.Kusto.ExecuteFunction)]
        [Description("Executes a user-defined Kusto function with named arguments on the regional Kusto cluster.")]
        public async Task<string> ExecuteFunction(
            [Description("The name of the user-defined function to execute.")] string functionName,
            [Description("The region of the Kusto cluster.")] string region,
            Dictionary<string, string> args)
        {
            var result = await _plugin.ExecuteFunctionAsync(functionName, region, args);
            return result.Result;
        }

        [KernelFunction(KernelFunctionNames.Kusto.ListKustoFunctions)]
        [Description("Lists all available Kusto functions in the specified region with metadata such as name and docstring.")]
        public async Task<string> ListFunctionsAsync(
            [Description("The region of the Kusto cluster to query for functions.")] string region)
        {
            var funcs = await _plugin.ListFunctionsAsync(region);
            return string.Join("\n", funcs.Select(f => $"- {f.Name}: {f.DocString}"));
        }

        public Dictionary<string, Func<Task<string>>> GetRegisteredFunctionDelegates()
        {
            var map = new Dictionary<string, Func<Task<string>>>();
            var functions = _plugin.ListFunctionsAsync("eastus").Result.Where(c => c.Name == "CappsRevisions");

            foreach (var func in functions)
            {
                var name = func.Name;
                var args = new Dictionary<string, string>(); // could be populated dynamically in the future

                map[name] = async () => (await _plugin.ExecuteFunctionAsync(name, "eastus", args)).Result;
            }

            return map;
        }
    }
}
