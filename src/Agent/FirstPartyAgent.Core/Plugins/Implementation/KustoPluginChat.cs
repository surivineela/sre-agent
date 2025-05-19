// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using IdentityModel.Client;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Plugins
{
    public class KustoPluginChat : IKustoPluginChat
    {
        private readonly IKustoPlugin _kustoPlugin;
        private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;
        private const int TokenLimit = 2000;

        public KustoPluginChat(IKustoPlugin kustoPlugin, IAgentOutboundCommunicationService agentOutboundCommunicationService)
        {
            _kustoPlugin = kustoPlugin;
            _agentOutboundCommunicationService = agentOutboundCommunicationService;
        }

        public async Task<string> ExecuteLocalFunctionOnClusterAsync(string functionName, string clusterName, string databaseName, Dictionary<string, string> args)
        {
            var fileName = Path.Combine(AppContext.BaseDirectory, "Plugins", "Definitions", "Queries", $"{functionName}.kql");
            KustoQueryResult queryResult;
            if (File.Exists(fileName))
            {
                var formatted = FormatQuery(args, fileName);
                queryResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, formatted, null);
            }
            else
            {
                throw new ArgumentException($"Function {functionName} not found in {fileName}");
            }

            if (queryResult.Result.Length > TokenLimit)
            {
                return "Query result row count is over thersholds a user should use sampling";
            }
            var msg = new ChatMessage(ChatRole.Tool, $"`{functionName}`{Environment.NewLine+Environment.NewLine}{queryResult.Message?.Text}");
            await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(ToolStatic.AsyncLocalThreadId.Value, string.Empty, msg);

            return queryResult.Result;
        }

        public async Task<string> ExecuteLocalFunctionAsync(string functionName, string region, Dictionary<string, string> args, SamplingOptions? samplingOptions = null)
        {
            region = region.NormalizeLocation();
            SamplingParameterHelper.AddSamplingParameters(args, samplingOptions);
            var fileName = Path.Combine(AppContext.BaseDirectory, "Plugins", "Definitions", "Queries", $"{functionName}.kql");
            KustoQueryResult queryResult;

            if (File.Exists(fileName))
            {
                var formatted = FormatQuery(args, fileName);
                queryResult = await _kustoPlugin.ExecuteKustoQuery(region, formatted);
            }
            else
            {
                queryResult = await _kustoPlugin.ExecuteFunctionAsync(functionName, region, args);
            }

            if (queryResult.Result.Length > TokenLimit)
            {
                return "Query result row count is over thersholds a user should use sampling";
            }

            var msg = new ChatMessage(ChatRole.Tool, $"`{functionName}`{Environment.NewLine+Environment.NewLine}{queryResult.Message?.Text}");
            await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(ToolStatic.AsyncLocalThreadId.Value, string.Empty, msg);

            return queryResult.Result;
        }

        private static string FormatQuery(Dictionary<string, string> args, string fileName)
        {
            var formatted = File.ReadAllText(fileName);
            foreach (var arg in args)
            {
                formatted = formatted.Replace($"##{arg.Key}##", arg.Value);
            }

            if (formatted.Contains("##"))
            {
                throw new Exception($"Not all placeholders were replaced in the query, {formatted}");
            }

            return formatted;
        }

        Task<KustoQueryResult> IKustoPlugin.ExecuteKustoQuery(string region, string query)
        {
            region = region.NormalizeLocation();
            return _kustoPlugin.ExecuteKustoQuery(region, query);
        }

        Task<KustoQueryResult> IKustoPlugin.ExecuteClusterKustoQuery(string cluster, string database, string fullQuery, DateTime? NowOverride)
        {
            return _kustoPlugin.ExecuteClusterKustoQuery(cluster, database, fullQuery, NowOverride);
        }

        Task<KustoQueryResult> IKustoPlugin.ExecuteFunctionAsync(string functionName, string region, Dictionary<string, string>? args)
        {
            region = region.NormalizeLocation();
            return _kustoPlugin.ExecuteFunctionAsync(functionName, region, args);
        }

        Task<List<KustoFunction>> IKustoPlugin.ListFunctionsAsync(string region)
        {
            region = region.NormalizeLocation();
            return _kustoPlugin.ListFunctionsAsync(region);
        }

        ChatMessage IKustoPlugin.CreateChatMessage(string query, string regionOrClusterUri, int count, int queryExecutionTimeInMilliSeconds, string? database, string? functionName)
        {
            return _kustoPlugin.CreateChatMessage(query, regionOrClusterUri, count, queryExecutionTimeInMilliSeconds, database, functionName);
        }
    }
}
