// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins
{
    public interface IAppInsightsPlugin
    {
        Task<string> ExecuteAppInsightsQuery(string resourceId, string queryString);
        Task<string> ExecuteLogAnalyticsQuery(string resourceId, string queryString, string timeSpan);
    }
}
