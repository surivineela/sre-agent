// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.SemanticKernel;

namespace Agent.Plugins.Interface
{
    public interface IAzureAlertingPlugin
    {
        Task<string> RunAlertKustoQuery(
            string impactStartDate,
            int monitoringIterationNumber,
            int monitoringGapInSeconds,
            string correlationId,
            string incidentTitle,
            string clusterName,
            string databaseName,
            bool useCorrelationIdForKustoQuery);
    }
}
