// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Data.Tools;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.KustoPlugin;
using Agent.Plugins.Tools;

namespace Agent.Plugins.Kusto.Tools
{
    [ToolTypeAttribute("KustoTool")]
    public class KustoToolType : IYamlToolAware
    {
        private readonly KustoPluginFactory _kustoFactory;
        private KustoToolDefinition? _definition;
        private readonly IConnectorResolver _connectorResolver;

        public KustoToolType(
            KustoPluginFactory kustoFactory,
            IConnectorResolver connectorResolver
            )
        {
            _kustoFactory = kustoFactory;
            _connectorResolver = connectorResolver;
        }

        public void SetToolDefinition(YamlToolDefinitionBase definition)
        {
            _definition = (KustoToolDefinition)definition;
        }

        public async Task<string> Run(string region, Dictionary<string, string> args)
        {
            string groupName = "ContainerApps";
            SamplingOptions? samplingOptions = null;

            if (_definition == null)
            {
                throw new InvalidOperationException("Tool definition was not set.");
            }
            
            if (string.IsNullOrEmpty(_definition.Connector))
            {
                throw new InvalidOperationException("Connector is not set in the tool definition.");
            }

            var connector = _connectorResolver.GetConnectorFromSettings<KustoConnector>(_definition.Connector);
            var kustoChat = _kustoFactory.Create(connector);

            switch (_definition.Mode)
            {
                case KustoExecutionMode.Function:
                    return await kustoChat.ExecuteLocalFunctionAsync(_definition.Function!, region, args, groupName, samplingOptions);

                case KustoExecutionMode.Query:
                    var formatedQuery = KustoPlugin.FormatQuery(_definition.Query!, args);

                    if (string.IsNullOrEmpty(region))
                    {
                        // TODO: Cleaner reference needed
                        // Region parameter will be not be configured with KustoConnector
                        return await kustoChat.ExecuteClusterKustoQuery(connector.ClusterUrl, connector.Database, formatedQuery);
                    }

                    var result = await kustoChat.ExecuteKustoQueryInternal(region, formatedQuery, groupName);
                    return result.Result;

                case KustoExecutionMode.Script:

                //var kustoChat = _kustoFactory.Create(_definition.GetConnector<KustoConnector>());
                //switch (_definition.Mode)
                //{
                //    case KustoExecutionMode.Function:

                //        return await kustoChat.ExecuteLocalFunctionAsync(_definition.Function ?? string.Empty, region, args, groupName, samplingOptions);

                default:
                    return string.Empty; // Return empty string for unsupported modes or unhandled cases

                    //}
                    ///  return string.Empty; // Return empty string for unsupported modes or unhandled cases
            }         //TODO unify kustosettings and kusto connector
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
            return await _kustoChat.ExecuteKustoQueryInternal(region, query, groupName);
        }
    }
}
