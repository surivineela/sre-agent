// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin]
    public class CdbSDKDiagnosePluginDefinition
    {
        private readonly ICdbSDKDiagnosePlugin _diagnose;
        public CdbSDKDiagnosePluginDefinition(ICdbSDKDiagnosePlugin diagnose) => _diagnose = diagnose;

        [Description("Analyze a Cosmos DB SDK diagnostics/error blob and return RCA JSON if available, otherwise an error JSON.")]
        public string SDKAnalyze(string error) =>
            _diagnose.SDKAnalyze(error);

        [Description("""
        Fetch Cosmos DB SDK diagnostics JSON from Application Insights traces. Searches for detailed diagnostic logs containing client configuration, system info, request stats, and transport timelines. Returns formatted diagnostic JSON entries in markdown with code blocks. Each entry typically contains: Summary, Client Configuration, System Info, Request Stats, and Transport Details.
        Example:
        
        # Cosmos DB SDK Diagnostic Logs Found
        Total diagnostic entries: 1

        ## Diagnostic Entry 1
        ```json
        {
            "Summary": {"DirectCalls": {"(201, 0)": 1}},
            "name": "CreateItemAsync",
            "duration in milliseconds": 1354.7767,
            "data": {
            "Client Configuration": {...},
            "System Info": {
                "systemHistory": [
                {
                    "cpu": 100.0,
                    "threadInfo": {
                    "isThreadStarving": "True",
                    "threadWaitIntervalInMs": 10451.2078
                    }
                }
                ]
            }
            }
        }
        ```
        """)]
        public async Task<string> FetchCosmosDbSdkError(
            [Description("The Application Insights resource ID in format '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Insights/components/{appInsightsName}' to query for Cosmos DB SDK telemetry.")]
            string appInsightsResourceId,
            [Description("Optional time span string (e.g., PT1H for 1 hour, PT24H for 24 hours, P3D for 3 days, P7D for 7 days) for the range of telemetry to query. Defaults to PT6H (last 6 hour).")]
            string? timeSpan = "PT6H") =>
            await _diagnose.FetchCosmosDbSdkError(appInsightsResourceId, timeSpan);

        [Description("Execute end-to-end Cosmos DB SDK diagnosis by fetching diagnostics from Application Insights and automatically analyzing them for root cause analysis. This function automatically: (1) fetches traces from Application Insights, (2) extracts and cleans Cosmos DB SDK diagnostic JSON, (3) analyzes each diagnostic entry for root cause, and (4) returns comprehensive structured results. Output includes: Status (success/partial_success/error), number of diagnostics analyzed, complete RCA for each diagnostic entry, and detailed findings and recommendations.")]
        public async Task<string> DiagnoseCosmosDbSdkErrors(
            [Description("The Application Insights resource ID in format '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Insights/components/{appInsightsName}' to query for Cosmos DB SDK telemetry.")]
            string appInsightsResourceId,
            [Description("Optional time span string (e.g., PT1H for 1 hour, PT24H for 24 hours, P3D for 3 days, P7D for 7 days) for the range of telemetry to query. Defaults to PT6H (last 6 hour).")]
            string? timeSpan = "PT6H") =>
            await _diagnose.DiagnoseCosmosDbSdkErrors(appInsightsResourceId, timeSpan);
    }
}
