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


        public async Task<string> Run(string kustoCluster, Dictionary<string, string> args)
        {


            if (_definition == null)
            {
                throw new InvalidOperationException("Tool definition was not set.");
            }

            if (string.IsNullOrEmpty(_definition.Connector))
            {
                throw new InvalidOperationException("Connector is not set in the tool definition.");
            }

            // Substitute parameters in connector name similar to FormatQuery, e.g. capps-##region## => capps-westeurope
            var parameterizedConnectorName = FormatConnectorName(_definition.Connector, args);

            var connector = _connectorResolver.GetConnectorFromSettings<KustoConnector>(parameterizedConnectorName, parameterizedConnectorName, kustoCluster);

            var kustoChat = _kustoFactory.Create(connector);

            // Determine if we should print the query based on tool definition and LLM-supplied args
            var printQuery = _definition.PrintQuery && args.GetValueOrDefault("printQuery", "true").ToLower() == "true";

            switch (_definition.Mode)
            {
                case KustoExecutionMode.Function:
                    return await kustoChat.ExecuteLocalFunctionOnClusterAsync(_definition.Function!, connector.ClusterUrl, _definition.Database, args);

                case KustoExecutionMode.Query:
                    // Region parameter is not used in Query mode, as the cluster is defined in the connector
                    var formatedQuery = KustoPlugin.FormatQuery(_definition.Query!, args);
                    return await kustoChat.ExecuteClusterKustoQuery(connector.ClusterUrl, connector.Database, formatedQuery, printQuery, _definition.Name);

                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Formats the connector name by substituting ##parameter## patterns with values from args dictionary,
        /// similar to how KustoPlugin.FormatQuery works.
        /// </summary>
        /// <param name="connectorName">The connector name template with ##parameter## patterns</param>
        /// <param name="args">Dictionary of parameter values</param>
        /// <returns>The connector name with parameters substituted</returns>
        private static string FormatConnectorName(string connectorName, Dictionary<string, string> args)
        {
            if (args == null || !args.Any())
            {
                return connectorName;
            }

            var formattedName = connectorName;
            foreach (var arg in args)
            {
                formattedName = formattedName.Replace($"##{arg.Key}##", arg.Value);
            }

            return formattedName;
        }

        [ToolTypeAttribute("KustoQuery")]
        public class KustoQuery
        {
            private readonly IKustoPlugin _kustoChat;

            public KustoQuery(IKustoPlugin kustoChat)
            {
                _kustoChat = kustoChat;
            }

            public async Task<KustoQueryResult> Run(string query, AzureRegion region, Dictionary<string, string> args, string groupName = "ContainerApps")
            {
                return await _kustoChat.ExecuteKustoQueryInternal(region, query, groupName);
            }
        }
    }
}
