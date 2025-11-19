// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Data.Tools;
using Agent.Framework;
using Agent.Plugins.Connector;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;

namespace Agent.Plugins.Tools;

[ToolType("KustoTool")]
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
        var parameterizedConnectorName = KustoPlugin.FormatTemplate(_definition.Connector, args);

        var connector = _connectorResolver.GetConnectorFromSettings<KustoConnector>(parameterizedConnectorName, parameterizedConnectorName, kustoCluster);

        var kustoChat = _kustoFactory.Create(connector);

        // Determine if we should print the query based on tool definition and LLM-supplied args
        var printQuery = _definition.PrintQuery && args.GetValueOrDefault("printQuery", "true").ToLower() == "true";

        switch (_definition.Mode)
        {
            case KustoExecutionMode.Function:
                var displayOptions = ConvertDisplayOptions(_definition.DisplayOptions);
                return await kustoChat.ExecuteLocalFunctionOnClusterAsync(
                    _definition.Function!,
                    connector.ClusterUrl,
                    _definition.Database,
                    args,
                    displayOptions,
                    toolDefinition: _definition);

            case KustoExecutionMode.Query:
                // Region parameter is not used in Query mode, as the cluster is defined in the connector
                var formatedQuery = KustoPlugin.FormatTemplate(_definition.Query!, args);
                return await kustoChat.ExecuteClusterKustoQuery(connector.ClusterUrl, string.IsNullOrEmpty(_definition.Database) ? connector.Database : _definition.Database, formatedQuery, printQuery, _definition.Name);

            default:
                return string.Empty;
        }
    }

    private static KustoDisplayOptions? ConvertDisplayOptions(KustoDisplayOptionsDefinition? definition)
    {
        if (definition is null)
        {
            return null;
        }

        return new KustoDisplayOptions
        {
            ShowTable = definition.ShowTable,
            ShowChart = definition.ShowChart,
            MaxTableRows = definition.MaxTableRows ?? 50,
            MaxChartPoints = definition.MaxChartPoints ?? 200,
            ChartTitle = definition.ChartTitle,
            XField = definition.XField,
            SeriesFields = definition.SeriesFields
        };
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
