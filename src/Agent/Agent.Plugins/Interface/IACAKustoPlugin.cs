// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.KustoPlugin;
using Microsoft.Extensions.AI;

namespace Agent.Plugins.Interface
{
    public interface IACAKustoPlugin
    {
        public Task<KustoQueryResult> ExecuteKustoQuery(string region, string query, string groupName = "ContainerApps");
        public Task<KustoQueryResult> ExecuteClusterKustoQuery(string cluster, string database, string fullQuery, DateTime? NowOverride);
        public Task<KustoQueryResult> ExecuteFunctionAsync(string functionName, string region, Dictionary<string, string>? args = null, string groupName = "ContainerApps");
        public Task<List<KustoFunctionInfo>> ListFunctionsAsync(string region);
        public ChatMessage CreateChatMessage(string query, string regionOrClusterUri, int count, int queryExecutionTimeInMilliSeconds, string? database = null, string? functionName = null, string groupName = "ContainerApps");
    }
}
