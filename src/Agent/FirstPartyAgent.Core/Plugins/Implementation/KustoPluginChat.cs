// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Core.Helpers;
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
        private readonly KustoClientService _kustoClientService;
        private const int TokenLimit = 2000;

        public KustoPluginChat(IKustoPlugin kustoPlugin, IAgentOutboundCommunicationService agentOutboundCommunicationService, KustoClientService kustoClientService)
        {
            _kustoPlugin = kustoPlugin;
            _agentOutboundCommunicationService = agentOutboundCommunicationService;
            _kustoClientService = kustoClientService;
        }

        public async Task<string> ExecuteLocalFunctionAsync(string functionName, string region, Dictionary<string, string> args, SamplingOptions? samplingOptions = null)
        {
            region = region.NormalizeLocation();
            SamplingParameterHelper.AddSamplingParameters(args, samplingOptions);
            var fileName = Path.Combine(AppContext.BaseDirectory, "Plugins", "Definitions", "Queries", $"{functionName}.kql");
            KustoQueryResult queryResult;

            if (File.Exists(fileName))
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

                queryResult = await _kustoPlugin.ExecuteKustoQuery(region, formatted);
            }
            else
            {
                queryResult = await _kustoPlugin.ExecuteFunctionAsync(functionName, region, args);
            }

            if (queryResult.RowCount > TokenLimit)
            {
                return "Query result row count is over thersholds a user should use sampling";
            }
            var msg = new ChatMessage(ChatRole.Tool, $"`{functionName}`\n\n" + queryResult.Message?.Text);
            await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(ToolStatic.AsyncLocalThreadId.Value, string.Empty, msg);

            return queryResult.Result;
        }

        Task<KustoQueryResult> IKustoPlugin.ExecuteKustoQuery(string region, string query)
        {
            region = region.NormalizeLocation();
            return _kustoPlugin.ExecuteKustoQuery(region, query);
        }

        Task<KustoQueryResult> IKustoPlugin.ExecuteClusterKustoQuery(string cluster, string database, string fullQuery, DateTime? NowOverride, Kernel kernel)
        {
            return _kustoPlugin.ExecuteClusterKustoQuery(cluster, database, fullQuery, NowOverride, kernel);
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

        ChatMessage IKustoPlugin.CreateChatMessage(string query, string regionOrClusterUri, int count, string? database, string? functionName)
        {
            return _kustoPlugin.CreateChatMessage(query, regionOrClusterUri, count, database, functionName);
        }
    }
}
