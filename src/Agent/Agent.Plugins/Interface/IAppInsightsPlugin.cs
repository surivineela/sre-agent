// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Interface
{
    public interface IAppInsightsPlugin
    {
        Task<string> QueryAppInsightsByResourceId(string appInsightsResourceId, string queryString, string? timeSpan = null, bool formatAsTsv = false);
        Task<string> QueryAppInsightsByAppId(string appInsightsAppId, string queryString, string? timeSpan = null, bool formatAsTsv = false);
        Task<string> QueryAppInsightsByWebAppSettings(string webAppResourceId, string queryString);

        Task<string> QueryLogAnalyticsByResourceId(string workspaceResourceId, string queryString, string? timeSpan = null, bool formatAsTsv = false);
        Task<string> QueryLogAnalyticsByWorkspaceId(string workspaceId, string queryString, string? timeSpan = null, bool formatAsTsv = false);
        Task<string> QueryLogAnalyticsByWebAppDiagnosticSettings(string resourceId, string queryString, string timeSpan);
    }
}
