// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Interface;
using Agent.Plugins.KustoPlugin;
using Agent.Plugins.Tools;
using Agent.Runtime.Reasoning.Models;

namespace Agent.Plugins.Kusto.Tools
{
    [ToolTypeAttribute("KustoFunction")]
    public class KustoFunction : IYamlToolAware
    {
        private readonly IKustoPluginChat _kustoChat;
        private KustoToolDefinition? _definition;

        public KustoFunction(IKustoPluginChat kustoChat)
        {
            _kustoChat = kustoChat;
        }

        public void SetToolDefinition(YamlToolDefinitionBase definition)
        {
            _definition = (KustoToolDefinition)definition;
        }

        public async Task<string> Run(string region, Dictionary<string, string> args)
        {
            string groupName = "ContainerApps";
            SamplingOptions? samplingOptions = null;
            if (_definition == null) throw new InvalidOperationException("Tool definition was not set.");

            return await _kustoChat.ExecuteLocalFunctionAsync(_definition.Function, region, args, groupName, samplingOptions);
        }
    }

    [ToolTypeAttribute("KustoQuery")]
    public class KustoQuery
    {
        private readonly IKustoPluginChat _kustoChat;

        public KustoQuery(IKustoPluginChat kustoChat)
        {
            _kustoChat = kustoChat;
        }

        public async Task<KustoQueryResult> Run(string query, string region, Dictionary<string, string> args, string groupName = "ContainerApps")
        {
            return await _kustoChat.ExecuteKustoQuery(query, region, groupName);
        }
    }
}
