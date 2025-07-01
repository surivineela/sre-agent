// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class AzureAlertingPluginDefinition
    {
        private readonly IAzureAlertingPlugin _azureAlertingPlugin;

        public AzureAlertingPluginDefinition(IAzureAlertingPlugin plugin)
        {
            _azureAlertingPlugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        [AgentTool(ToolMode.Auto)]
        [Description("Runs the kusto query for the alert and returns the result.")]
        public Task<string> RunAlertKustoQuery(
            [Description("ImpactStartDate from the Incident Details")] string impactStartDate,
            [Description("Monitoring Iteration Number")] int monitoringIterationNumber,
            [Description("Monitoring Gap In Seconds")] int monitoringGapInSeconds,
            [Description("Correlation Id")] string correlationId,
            [Description("Incident Title")] string incidentTitle,
            [Description("Kusto Cluster Name")] string clusterName,
            [Description("Kusto Database Name")] string databaseName,
            [Description("Use Correlation Id for Kusto Query")] bool useCorrelationIdForKustoQuery)
        {
            return _azureAlertingPlugin.RunAlertKustoQuery(impactStartDate, monitoringIterationNumber, monitoringGapInSeconds, correlationId, incidentTitle, clusterName, databaseName, useCorrelationIdForKustoQuery);
        }
    }
}
