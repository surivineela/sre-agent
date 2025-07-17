// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Agent.Framework;
using Agent.Plugins.Interface;
using Microsoft.SemanticKernel;
using Agent.Core.Models.Charts;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin]
    public class WebAppRestartPluginDefinition(IWebAppRestartPlugin webAppRestartPlugin)
    {
        private readonly IWebAppRestartPlugin _webAppRestartPlugin = webAppRestartPlugin;

        [KernelFunction("get_web_app_restart_execution")]
        [Description("Gets comprehensive restart execution data and insights for an Azure Web App")]
        public async Task<string> GetWebAppRestartExecution(
            [Description("The full Azure resource ID of the Web App to analyze")] string resourceId)
        {
            return await _webAppRestartPlugin.GetWebAppRestartExecution(resourceId);
        }

        [KernelFunction("get_web_app_call_stacks")]
        [Description("Gets call stack information for Azure Web App executions")]
        public async Task<string> GetWebAppCallStacks(
            [Description("The full Azure resource ID of the Web App to analyze")] string resourceId)
        {
            return await _webAppRestartPlugin.GetWebAppCallStacks(resourceId);
        }

        [KernelFunction("get_failed_request_invocations")]
        [Description("Gets a summary of failed request invocations for an Azure Web App")]
        public async Task<IReadOnlyList<FailedRequestsTimeSeriesData>> GetFailedRequestInvocations(
            [Description("The full Azure resource ID of the Web App to analyze")] string resourceId,
            [Description("Optional duration in minutes to query for (defaults to 90 minutes)")] int? minutes = null)
        {
            return await _webAppRestartPlugin.GetFailedRequestInvocations(resourceId, minutes);
        }

        [KernelFunction("get_top3_exceptions")]
        [Description("Gets the top 3 exceptions for an Azure Web App")]
        public async Task<string> GetTop3Exceptions(
            [Description("The full Azure resource ID of the Web App to analyze")] string resourceId,
            [Description("Optional start time for the query (defaults to 1 hour ago)")] DateTime? startTime = null,
            [Description("Optional end time for the query (defaults to current time minus 15 minutes)")] DateTime? endTime = null)
        {
            return await _webAppRestartPlugin.GetTop3Exceptions(resourceId, startTime, endTime);
        }

        [KernelFunction("is_web_app")]
        [Description("Checks if a resource is a Web App by verifying its 'kind' property")]
        public async Task<bool> IsWebApp(
            [Description("The full Azure resource ID to check")] string resourceId)
        {
            return await _webAppRestartPlugin.IsWebApp(resourceId);
        }
    }
}
