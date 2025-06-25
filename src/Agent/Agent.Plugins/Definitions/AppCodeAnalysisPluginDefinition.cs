// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Attributes;
using Agent.Core.Helpers;
using Agent.Framework;
using Agent.Plugins.Interface;
using Azure.ResourceManager.ResourceGraph.Models;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin]
    public class AppCodeAnalysisPluginDefinition
    {

        private IAppCodeAnalysisPlugin _appCodeAnalysisPlugin;


        public AppCodeAnalysisPluginDefinition(IAppCodeAnalysisPlugin appCodeAnalysisPlugin)
        {
            _appCodeAnalysisPlugin = appCodeAnalysisPlugin;
        }

        [KernelFunction("get_app_stack_trace")]
        [Description("This function attempts to retrieve the stack traces for a user's particular app")]
        public async Task<string> GetCallStackForApp(
        [Description("resourceId of the app")] string resourceId)
        {
            return await _appCodeAnalysisPlugin.GetCallStackForApp(resourceId);
        }



        [KernelFunction("Makes the application wait for some time")]
        [Description("This function forces a delay for the application to trigger a wait")]
        public async Task<bool> WaitInMilliSeconds(
        [Description("time to wait in milliseconds")] int numMilliSeconds)
        {

            return await _appCodeAnalysisPlugin.WaitInMilliSeconds(numMilliSeconds); 
        }


        [KernelFunction("get_summary_of_app_exceptions")]
        [Description("This function retrieves the summary of the exceptions on the app")]
        public async Task<string> GetSummaryOfExceptions(
            [Description("resourceId of the app")] string resourceId)
        {
            return await _appCodeAnalysisPlugin.GetSummaryOfExceptions(resourceId);
        }

        [KernelFunction("get_stack_trace_of_recent_exception")]
        [Description("This function retrieves the stack trace of the most recent exception")]
        public async Task<string> GetStackTraceOfLastException(
            [Description("resourceId of the app")] string resourceId)
        {
            return await _appCodeAnalysisPlugin.GetStackTraceOfLastException(resourceId);
        }

        
        [KernelFunction("get_stack_trace_of_most_common_exception")]
        [Description("This function retrieves the stack trace of the most recent exception")]
        public async Task<string> GetStackTraceOfMostCommonException(
            [Description("resourceId of the app")] string resourceId)
        {
            return await _appCodeAnalysisPlugin.GetStackTraceOfMostCommonException(resourceId);
        }

        [Description("This function retrieves the stack traces of the n most common app exceptions")]
        public async Task<string> GetStackTracesOfNMostCommonExceptions(
            [Description("resourceId of the app")] string resourceId,
            [Description("number of distinct most common exceptions")] int num)
        {
            return await _appCodeAnalysisPlugin.GetStackTracesOfNMostCommonExceptions(resourceId, num);
        }

        [WriteAction]
        [RequiresApproval]
        [KernelFunction("perform_deployment_swap_for_app")]
        [Description("Performs a Deployment Swap for the specified app.")]
        public async Task<string> PerformDeploymentSwapForApp(
           [Description("resourceId for app")] string resourceId)
        {
          return await _appCodeAnalysisPlugin.PerformDeploymentSwapForApp(resourceId);
        }

        [KernelFunction("get_deployment_activity_for_app")]
        [Description("Gets Deployment Activities on the specified app")]
        public async Task<string> GetDeploymentActivity(
        [Description("resourceId for app")] string resourceId)
        {
           return await _appCodeAnalysisPlugin.GetDeploymentActivity(resourceId);   
        }

        [KernelFunction("get_app_console_logs")]
        [Description("This function attempts to retrieve error messages in the console logs and platform logs from a user's particular app")]
        public async Task<string> GetAppConsoleLogs(
        [Description("resourceId of the app")] string resourceId)
        {
            return await _appCodeAnalysisPlugin.GetAppConsoleLogs(resourceId);
        }

        [Description("This function retrieves the link to the Applens web app down analysis")]
        public string GetWebAppDownAnalysisLink(
            [Description("resourceId of the app")] string resourceId)
        {
            return _appCodeAnalysisPlugin.GetWebAppDownAnalysisLink(resourceId);
        }
    }
}
