// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Plugins
{
    public interface IKustoPlugin
    {
        public Task<string> ExecuteKustoQuery(string region, string query);
        public Task<string> ExecuteClusterKustoQuery(string cluster, string database, string fullQuery, DateTime? NowOverride, Kernel kernel);
        public Task<string> ExecuteFunctionAsync(string functionName, string region, Dictionary<string, string>? args = null);
        public Task<List<KustoFunction>> ListFunctionsAsync(string region);
        public Task<string> ExecuteLocalFunctionAsync(string functionName, string region, Dictionary<string, string> args);
        public ChatMessage CreateChatMessage(string query, string regionOrClusterUri, string database = null, string functionName = null);
    }
}
