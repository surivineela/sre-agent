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
        // Primary methods using AzureRegion enum for type safety
        public Task<string> ExecuteLocalFunctionAsync(string functionName, AzureRegion region, Dictionary<string, string> args, string? groupName = null, SamplingOptions? samplingOptions = null);
        public Task<string> ExecuteLocalFunctionOnClusterAsync(string functionName, string clusterName, string databaseName, Dictionary<string, string> args, KustoDisplayOptions? displayOptions = null);
        
        public Task<string> ExecuteKustoQuery(AzureRegion region, string query, string? groupName = null);
        public Task<string> ExecuteFunctionAsync(string functionName, AzureRegion region, Dictionary<string, string>? args = null, string? groupName = null);
        public Task<List<KustoFunctionInfo>> ListFunctionsAsync(AzureRegion region);
        
        // Internal methods - keeping string for internal implementation flexibility
        Task<KustoQueryResult> ExecuteClusterKustoQueryInternal(
            string cluster,
            string database,
            string fullQuery
        );
        Task<KustoQueryResult> ExecuteKustoQueryInternal(
            AzureRegion region,
            string query,
            string? groupName = null 
        );
        Task<KustoQueryResult> ExecuteFunctionInternalAsync(
            string functionName,
            AzureRegion region,
            Dictionary<string, string>? args = null,
            string? groupName = null);
        
        public Task<string> ExecuteClusterKustoQuery(string cluster, string database, string fullQuery);
        public ChatMessage CreateChatMessage(string query, string regionOrClusterUri, int count, int queryExecutionTimeInMilliSeconds, string? database = null, string? functionName = null, string? groupName = null);
    }
}
