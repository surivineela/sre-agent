using System;
using System.Collections.Generic;
using System.ComponentModel;
using Agent.Core.Helpers;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;

namespace Agent.Plugins;

public class AppInsightsPlugin : IAppInsightsPlugin
{
    private readonly ArmHelper _armHelper;

    public AppInsightsPlugin(ArmHelper armHelper)
    {
        _armHelper = armHelper;
    }

    [KernelFunction("query_app_insights")]
    [Description("Queries Application Insights")]
    public async Task<string> ExecuteAppInsightsQuery(
    [Description("query for Application Insights API call")] string queryString)
    {
        try
        {
            var results = await _armHelper.ExecuteAppInsightsQuery(queryString);
            return results;
        }
        catch (Exception ex) {
            return $"The Application Insights query {queryString} failed due to the exception {ex.Message}.";
        }
    }
}

