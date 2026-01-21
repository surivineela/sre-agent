// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Data.Tools;
using Agent.Framework;
using Agent.Plugins.Connector;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;
using Agent.Plugins.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Plugins.Kusto.Tools
{
    /// <summary>
    /// Factory for creating KustoTool instances.
    /// </summary>
    [ToolType("KustoTool")]
    public class KustoToolExecutorFactory : IYamlToolExecutorFactory
    {
        public IYamlToolExecutor Create(YamlToolDefinitionBase definition, IServiceProvider serviceProvider)
        {
            var kustoFactory = serviceProvider.GetRequiredService<KustoPluginFactory>();
            var connectorResolver = serviceProvider.GetRequiredService<IConnectorResolver>();
            var kustoDefinition = (KustoToolDefinition)definition;

            return new KustoTool(
                kustoFactory,
                connectorResolver,
                kustoDefinition);
        }
    }

    /// <summary>
    /// Kusto tool implementation that extends YamlToolExecutor.
    /// Uses factory pattern for instantiation and provides clean ExecuteAsync interface.
    /// </summary>
    public class KustoTool : YamlToolExecutor<KustoToolDefinition>
    {
        private readonly KustoPluginFactory _kustoFactory;
        private readonly IConnectorResolver _connectorResolver;

        public KustoTool(
            KustoPluginFactory kustoFactory,
            IConnectorResolver connectorResolver,
            KustoToolDefinition definition) : base(definition)
        {
            _kustoFactory = kustoFactory;
            _connectorResolver = connectorResolver;
        }

        public override async Task<object?> ExecuteAsync(string threadId, AIFunctionArguments parameters)
        {
            // Convert AIFunctionArguments to Dictionary<string, string> using base class helper
            var paramsDict = ConvertToStringDictionary(parameters);

            // Get connector - substitute parameters in connector name if needed
            var connector = GetConnector(paramsDict, "");
            var kustoPlugin = _kustoFactory.Create(connector);

            // Determine if we should print the query
            var printQuery = ToolDefinition.PrintQuery && paramsDict.GetValueOrDefault("printQuery", "true").ToLower() == "true";

            // Execute based on mode
            return ToolDefinition.Mode switch
            {
                KustoExecutionMode.Function => await ExecuteFunction(kustoPlugin, connector, paramsDict),
                KustoExecutionMode.Query => await ExecuteQuery(kustoPlugin, connector, paramsDict, printQuery),
                KustoExecutionMode.Script => await ExecuteScript(kustoPlugin, connector, paramsDict, printQuery),
                _ => throw new InvalidOperationException($"Unsupported execution mode: {ToolDefinition.Mode}")
            };
        }

        private async Task<string> ExecuteFunction(IKustoPlugin kustoPlugin, KustoConnector connector, Dictionary<string, string> parameters)
        {
            // Build display options from tool definition, auto-detecting | render if present
            var displayOptions = BuildDisplayOptions(ToolDefinition.Query);

            return await kustoPlugin.ExecuteLocalFunctionOnClusterAsync(
                ToolDefinition.Function!,
                connector.ClusterUrl,
                ToolDefinition.Database,
                parameters,
                displayOptions: displayOptions,
                toolDefinition: ToolDefinition);
        }

        private async Task<string> ExecuteQuery(IKustoPlugin kustoPlugin, KustoConnector connector, Dictionary<string, string> parameters, bool printQuery)
        {
            var query = ToolDefinition.Query
                ?? throw new InvalidOperationException("Query is required for Query mode");

            var formattedQuery = KustoPlugin.FormatTemplate(query, parameters);

            var database = string.IsNullOrEmpty(ToolDefinition.Database)
                ? connector.Database
                : ToolDefinition.Database;

            // Build display options, auto-detecting | render if present
            var displayOptions = BuildDisplayOptions(formattedQuery);

            return await kustoPlugin.ExecuteClusterKustoQuery(
                connector.ClusterUrl,
                database,
                formattedQuery,
                printQuery,
                ToolDefinition.Name,
                displayOptions);
        }

        private async Task<string> ExecuteScript(IKustoPlugin kustoPlugin, KustoConnector connector, Dictionary<string, string> parameters, bool printQuery)
        {
            var scriptFile = ToolDefinition.File
                ?? throw new InvalidOperationException("File is required for Script mode");

            if (!File.Exists(scriptFile))
            {
                throw new FileNotFoundException($"Script file not found: {scriptFile}");
            }

            var scriptContent = await File.ReadAllTextAsync(scriptFile);
            var formattedScript = KustoPlugin.FormatTemplate(scriptContent, parameters);

            var database = string.IsNullOrEmpty(ToolDefinition.Database)
                ? connector.Database
                : ToolDefinition.Database;

            // Build display options, auto-detecting | render if present
            var displayOptions = BuildDisplayOptions(formattedScript);

            return await kustoPlugin.ExecuteClusterKustoQuery(
                connector.ClusterUrl,
                database,
                formattedScript,
                printQuery,
                ToolDefinition.Name,
                displayOptions);
        }

        /// <summary>
        /// Builds KustoDisplayOptions from the tool definition, auto-enabling ShowChart if query contains "| render".
        /// YAML definition settings always take precedence over auto-detection.
        /// </summary>
        private KustoDisplayOptions BuildDisplayOptions(string? query)
        {
            var def = ToolDefinition.DisplayOptions;

            // extract chart type from query, returns null if no render operator
            var chartType = KustoDisplayFormatter.GetChartType(query ?? string.Empty);
            bool shouldShowChart;
            bool shouldShowTable;

            if (chartType == null)
            {
                shouldShowChart = def?.ShowChart ?? false;
                shouldShowTable = def?.ShowTable ?? false;
            }
            else if (chartType == "table")
            {
                shouldShowChart = def?.ShowChart ?? false;
                shouldShowTable = def?.ShowTable ?? true;
            }
            else
            {
                shouldShowChart = def?.ShowChart ?? true;
                shouldShowTable = def?.ShowTable ?? false;
            }

            return new KustoDisplayOptions
            {
                ShowChart = shouldShowChart,
                ChartType = chartType,
                ShowTable = shouldShowTable,
                MaxTableRows = def?.MaxTableRows ?? 50,
                MaxChartPoints = def?.MaxChartPoints ?? 200,
                ChartTitle = def?.ChartTitle,
                XField = def?.XField,
                SeriesFields = def?.SeriesFields
            };
        }

        private KustoConnector GetConnector(Dictionary<string, string> parameters, string kustoCluster)
        {
            var parameterizedConnectorName = KustoPlugin.FormatTemplate(ToolDefinition.Connector, parameters);

            return _connectorResolver.GetConnectorFromSettings<KustoConnector>(
                parameterizedConnectorName,
                parameterizedConnectorName,
                kustoCluster);
        }
    }
}
