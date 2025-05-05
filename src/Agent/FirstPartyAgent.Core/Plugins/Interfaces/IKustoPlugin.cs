// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Plugins
{
    public interface IKustoPlugin
    {
        public Task<string> ExecuteKustoQuery(string region, string query, bool displayQuery = true);
        public Task<string> ExecuteClusterKustoQuery(string cluster, string database, string fullQuery, DateTime? NowOverride, Kernel kernel, bool displayQuery = true);
        public Task<string> ExecuteFunctionAsync(string functionName, string region, Dictionary<string, string>? args = null, bool displayQuery = true);
        public Task<List<KustoFunction>> ListFunctionsAsync(string region);
        public Task<string> ExecuteLocalFunctionAsync(string functionName, string region, Dictionary<string, string> args);
    }
}
