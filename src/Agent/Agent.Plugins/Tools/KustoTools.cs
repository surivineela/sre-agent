// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Interface;
using Agent.Plugins.KustoPlugin;
using Agent.Plugins.Tools;
using Agent.Runtime.Reasoning.Models;

namespace Agent.Plugins.Kusto.Tools
{
    [ToolTypeAttribute("KustoTool")]
    public class KustoToolType : IYamlToolAware
    {
        private readonly KustoPluginFactory _kustoFactory;
        private KustoToolDefinition? _definition;

        public KustoToolType(KustoPluginFactory kustoFactory)
        {
            _kustoFactory = kustoFactory;
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
            var kustoChat = _kustoFactory.Create(_definition.GetConnector<KustoConnector>());
            switch (_definition.Mode)
            {
                case KustoExecutionMode.Function:

                    return await kustoChat.ExecuteLocalFunctionAsync(_definition.Function, region, args, groupName, samplingOptions);

                case KustoExecutionMode.Query:
                    var formmatedQuery = KustoPlugin.FormatQuery(_definition.Query, args);
                    var result = await kustoChat.ExecuteKustoQuery(region, formmatedQuery, groupName);
                    return result.Result;

                case KustoExecutionMode.Script:
                    break;

                default:
                    break;
            }
            return string.Empty; // Return empty string for unsupported modes or unhandled casesz   
            //TODO unify kustosettings and kusto connector
        }
    }

    [ToolTypeAttribute("KustoQuery")]
    public class KustoQuery
    {
        private readonly IKustoPlugin _kustoChat;

        public KustoQuery(IKustoPlugin kustoChat)
        {
            _kustoChat = kustoChat;
        }

        public async Task<KustoQueryResult> Run(string query, string region, Dictionary<string, string> args, string groupName = "ContainerApps")
        {
            return await _kustoChat.ExecuteKustoQuery(query, region, groupName);
        }
    }
}
