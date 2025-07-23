// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Kusto;
using Agent.Plugins.KustoPlugin;
using Microsoft.Extensions.AI;

namespace Agent.Plugins.Interface
{
    /// <summary>
    /// Print Kusto queries in chat messages
    /// </summary>
    public interface IKustoPlugin
    {
        public Task<string> ExecuteLocalFunctionAsync(string functionName, string region, Dictionary<string, string> args, string? groupName = null, SamplingOptions? samplingOptions = null);
        public Task<string> ExecuteLocalFunctionOnClusterAsync(string functionName, string clusterName, string databaseName, Dictionary<string, string> args);
        public Task<KustoQueryResult> ExecuteKustoQuery(string region, string query, string? groupName = null);
        public Task<KustoQueryResult> ExecuteClusterKustoQuery(string cluster, string database, string fullQuery, DateTime? NowOverride);
        public Task<string> ExecuteClusterKustoQuery(string cluster, string database, string fullQuery);

        public Task<KustoQueryResult> ExecuteFunctionAsync(string functionName, string region, Dictionary<string, string>? args = null, string? groupName = null);
        public Task<List<KustoFunctionInfo>> ListFunctionsAsync(string region);
        public ChatMessage CreateChatMessage(string query, string regionOrClusterUri, int count, int queryExecutionTimeInMilliSeconds, string? database = null, string? functionName = null, string? groupName = null);

    }
}
