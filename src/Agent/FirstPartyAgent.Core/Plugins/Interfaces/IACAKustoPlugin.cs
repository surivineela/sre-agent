// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Plugins
{
    public interface IACAKustoPlugin
    {
        public Task<KustoQueryResult> ExecuteKustoQuery(string region, string query);
        public Task<KustoQueryResult> ExecuteClusterKustoQuery(string cluster, string database, string fullQuery, DateTime? NowOverride);
        public Task<KustoQueryResult> ExecuteFunctionAsync(string functionName, string region, Dictionary<string, string>? args = null);
        public Task<List<KustoFunction>> ListFunctionsAsync(string region);
        public ChatMessage CreateChatMessage(string query, string regionOrClusterUri, int count, int queryExecutionTimeInMilliSeconds, string? database = null, string? functionName = null);
    }
}
