// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Agent.Plugins.Definitions;

// [Export]
public class AppInsightsPluginDefinition
{
    private IAppInsightsPlugin _appInsightsPlugin;

    public AppInsightsPluginDefinition(IAppInsightsPlugin appInsightsPlugin)
    {
        _appInsightsPlugin = appInsightsPlugin;
    }

    [KernelFunction("make_app_insight_api_call")]
    [Description("Makes an api call to application insights")]
    public async Task<string> ExecuteAppInsightsQuery(
        string resourceId, 
       [Description("query for api call to application insights")] string queryString)
    {
        return await _appInsightsPlugin.ExecuteAppInsightsQuery(resourceId, queryString);
    }
}
