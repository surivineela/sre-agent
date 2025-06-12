using System;
using System.Collections.Generic;
using System.ComponentModel;
using Agent.Core;
using Agent.Core.Attributes;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Plugins.Interface;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;

namespace Agent.Plugins.Implementation;

public class AppCodeAnalysisPlugin : IAppCodeAnalysisPlugin
{
    private readonly ArmHelper _armHelper;
    private IAppInsightsPlugin _appInsightsPlugin;

    public Guid? ThreadId { get; set; }


    public AppCodeAnalysisPlugin(ArmHelper armHelper, IAppInsightsPlugin appInsightsPlugin)
    {
        _armHelper = armHelper;
        _appInsightsPlugin = appInsightsPlugin;
    }

    [KernelFunction("get_app_stack_trace")]
    [Description("This function attempts to retrieve the stack traces from an apps' failures and exceptions")]
    public async Task<string> GetCallStackForApp(
    [Description("resourceId of the app")] string resourceId)
    {
        DateTime startTime = DateTime.UtcNow.AddDays(-1);
        DateTime endTime = DateTime.UtcNow;
        
        string resourceName = resourceId;
        if (resourceId.Contains('/'))
        {
            var splitResourceParts = resourceId.Split('/');
            resourceName = splitResourceParts[splitResourceParts.Length - 1];
        }

        string stackTraceQuery = $@"exceptions
        | where timestamp >= datetime({startTime.ToString("O")}) and timestamp <= datetime({endTime.ToString("O")})
        | where cloud_RoleName =~ ""{resourceName}"" or cloud_RoleName startswith ""{resourceName}-""
        | project timestamp, type, method, outerMethod, details, customDimensions, operation_Name
        | top 10 by timestamp desc";

        var stackTrace = await _appInsightsPlugin.ExecuteAppInsightsQuery(resourceId, stackTraceQuery);
        return stackTrace;
    }

    [KernelFunction("Makes the application wait for some time")]
    [Description("This function forces a delay for the application to trigger a wait")]
    public async Task<bool> WaitInMilliSeconds(
        [Description("time to wait in milliseconds")] int numMilliSeconds)
    {
         await Task.Delay(numMilliSeconds);
        return true; 
    }

    [Description("This function retrieves the link to the Applens web app down analysis")]
    public string GetWebAppDownAnalysisLink(
        [Description("The resourceId of the app")] string resourceId)
    { 
        var endTime = DateTime.UtcNow;
        var startTime = endTime.AddDays(-1);

        string endString = endTime.ToString("yyyy-MM-dd HH:mm");
        string startString = endTime.ToString("yyyy-MM-dd HH:mm");
        
        string applensLink = $"https://applens.trafficmanager.net{resourceId}/analysis/appDownAnalysis?startTime={startString}&endTime={endString}";
        return applensLink;
    }


    [KernelFunction("get_summary_of_app_exceptions")]
    [Description("This function retrieves the summary of the exceptions on the app")]
    public async Task<string> GetSummaryOfExceptions(
    [Description("resourceId of the app")] string resourceId)
    {
        string resourceName = resourceId;
        if (resourceId.Contains('/'))
        {
            var splitResourceParts = resourceId.Split('/');
            resourceName = splitResourceParts[splitResourceParts.Length - 1];
        }
    
        string query = $@"exceptions
        | where timestamp >= ago(1d)
        | where cloud_RoleName =~ ""{resourceName}"" or cloud_RoleName startswith ""{resourceName}-""
        | extend Exception = strcat(type,"": "" ,outerMessage)
        | summarize count() by Exception";

        var results = await _appInsightsPlugin.ExecuteAppInsightsQuery(resourceId, query);
        return results;
    }

    [KernelFunction("get_stack_trace_of_recent_exception")]
    [Description("This function retrieves the stack trace of the most recent exception")]
    public async Task<string> GetStackTraceOfLastException(
    [Description("resourceId of the app")] string resourceId)
    {
        string resourceName = resourceId;
        if (resourceId.Contains('/'))
        {
            var splitResourceParts = resourceId.Split('/');
            resourceName = splitResourceParts[splitResourceParts.Length - 1];
        }

        string query = $@"exceptions 
        | where timestamp > ago(1d)
        | where cloud_RoleName =~ ""{resourceName}"" or cloud_RoleName startswith ""{resourceName}-""
        | top 1 by timestamp desc
        | project ExceptionMessage = outerMessage, ExceptionType = outerType, ParsedStack = details[0].parsedStack  
        | mv-expand StackFrame = ParsedStack  
        | extend MethodNameWithLine = strcat(tostring(split(StackFrame.method, "","")[0]),   
                                             "": line "",   
                                             tostring(StackFrame.line))  
        | summarize FullStackTrace = make_list(MethodNameWithLine) by ExceptionMessage, ExceptionType  
        | extend FullStackTraceString = strcat_array(FullStackTrace, ""\n"")  
        | project ExceptionMessage, ExceptionType, FullStackTraceString";


        var results = await _appInsightsPlugin.ExecuteAppInsightsQuery(resourceId, query); 
        
        return results;
    }

    [KernelFunction("get_stack_trace_of_most_common_exception")]
    [Description("This function retrieves the stack trace of the most common app exception")]
    public async Task<string> GetStackTraceOfMostCommonException(
    [Description("resourceId of the app")] string resourceId)
    {
        string resourceName = resourceId;
        if (resourceId.Contains('/'))
        {
            var splitResourceParts = resourceId.Split('/');
            resourceName = splitResourceParts[splitResourceParts.Length - 1];
        }

        string query = $@"exceptions  
        | where timestamp > ago(1d)
        | where * contains ""{resourceName}""
        | summarize Count = count() by ExceptionMessage = outerMessage, ExceptionType = outerType, ParsedStack = tostring(details[0].parsedStack)
        | order by Count desc  
        | take 1  
        | mv-expand StackFrame = todynamic(ParsedStack) 
        | extend MethodNameWithLine = strcat(tostring(split(StackFrame.method, "","")[0]),  
                                             "": line "",  
                                             tostring(StackFrame.line))  
        | summarize FullStackTrace = make_list(MethodNameWithLine) by ExceptionMessage, ExceptionType  
        | extend FullStackTraceString = strcat_array(FullStackTrace, ""\n"")  
        | project ExceptionMessage, ExceptionType, FullStackTraceString ";


        var results = await _appInsightsPlugin.ExecuteAppInsightsQuery(resourceId, query);

        return results;
    }

    [Description("This function retrieves the stack traces of the n most common app exceptions")]
    public async Task<string> GetStackTracesOfNMostCommonExceptions(
    [Description("resourceId of the app")] string resourceId,
    [Description("number of distinct most common exceptions")] int num)
    {
        string resourceName = resourceId;
        if (resourceId.Contains('/'))
        {
            var splitResourceParts = resourceId.Split('/');
            resourceName = splitResourceParts[splitResourceParts.Length - 1];
        }

        string query = $@"exceptions  
        | where timestamp > ago(1d)
        | where * contains ""{resourceName}""
        | summarize Count = count() by ExceptionMessage = outerMessage, ExceptionType = outerType, ParsedStack = tostring(details[0].parsedStack)
        | order by Count desc
        | summarize arg_max(Count, *) by ExceptionMessage, ExceptionType
        | top {num} by Count
        | mv-expand StackFrame = todynamic(ParsedStack) 
        | extend MethodNameWithLine = strcat(tostring(split(StackFrame.method, "","")[0]),  
                                                "": line "",  
                                                tostring(StackFrame.line))  
        | summarize FullStackTrace = make_list(MethodNameWithLine) by ExceptionMessage, ExceptionType 
        | extend FullStackTraceString = strcat_array(FullStackTrace, ""\n"")  
        | project ExceptionMessage, ExceptionType, FullStackTraceString";

        var results = await _appInsightsPlugin.ExecuteAppInsightsQuery(resourceId, query);

        return results;
    }

    [KernelFunction("get_app_console_logs")]
    [Description("This function attempts to retrieve error messages from an app's console logs and platform logs")]
    public async Task<string> GetAppConsoleLogs(
    [Description("resourceId of the app")] string resourceId)
    {
        DateTime startTime = DateTime.UtcNow.AddDays(-1);
        DateTime endTime = DateTime.UtcNow;

        string resourceName = resourceId;
        if (resourceId.Contains('/'))
        {
            var splitResourceParts = resourceId.Split('/');
            resourceName = splitResourceParts[splitResourceParts.Length - 1];
        }

        string consoleLogsQuery = $@"let start = datetime({startTime.ToString("O")});
        let end = datetime({endTime.ToString("O")});
        AppServiceHTTPLogs
        | where TimeGenerated between(start .. end)
        | where _ResourceId has '{resourceName}'
        | where ScStatus >= 500
        | project TimeGenerated, ScStatus, CsMethod, CsUriStem
        | union (AppServicePlatformLogs 
            | where TimeGenerated between(start .. end)
            | where _ResourceId has '{resourceName}'
            | parse-where Message with 'Image ' DockerImage ' is pulled from registry ' ImageRegistry
            | project TimeGenerated, Level, Message)
        | union (AppServiceConsoleLogs 
            | where TimeGenerated between(start .. end)
            | where _ResourceId has '{resourceName}'
            | where Level != 'Informational'
            | project TimeGenerated, Level, Message = ResultDescription)
        | order by TimeGenerated asc";

        var consoleLogs = await _appInsightsPlugin.ExecuteLogAnalyticsQuery(resourceId, consoleLogsQuery, "P1D");
        return consoleLogs;
    }

    [KernelFunction("perform_deployment_swap_for_app")]
    [Description("Performs a Deployment Swap for the specified app")]
    public async Task<string> PerformDeploymentSwapForApp(
    [Description("resourceId for app")] string resourceId)
    {
        // Parse subscriptionId and resourceGroupName from resourceId  
        var segments = resourceId.Split('/');
        if (segments.Length < 5)
        {
            throw new ArgumentException("Invalid resource ID format.");
        }

        string subscriptionId = segments[2];
        string resourceGroupName = segments[4];

        var (deployments, swaps) = await _armHelper.GetDeploymentActivity(subscriptionId, resourceGroupName, resourceId);

        string sourceSlot = swaps[0].ResourceId.Split('/').Last();
        string targetSlot = "production";
        bool preserveVNet = true;

        var success = await _armHelper.SwapAppServiceSlotsAsync(resourceId, preserveVNet, sourceSlot, targetSlot);
        
        if (success)
        {
            return $"The deployment swap operation has successfully completed. Swap operations were performed from {sourceSlot} to {targetSlot}";
        }
        return "There was an issue performing the swap. The deployment swap operation(s) was unsuccessful.";
    }

    [KernelFunction("get_deployment_activity_for_app")]
    [Description("Gets Deployment Activities on the specified app")]
    public async Task<string> GetDeploymentActivity(
    [Description("resourceId for app")] string resourceId)
    {
        try
        {
            // Parse subscriptionId and resourceGroupName from resourceId  
            var segments = resourceId.Split('/');
            if (segments.Length < 5)
            {
                throw new ArgumentException("Invalid resource ID format.");
            }

            string subscriptionId = segments[2];
            string resourceGroupName = segments[4];

            // Call the method to get deployment activities  
            var (deployments, swaps) = await _armHelper.GetDeploymentActivity(subscriptionId, resourceGroupName, resourceId);

            // Initialize result string  
            string result = "Deployment Activities:\n";

            if (deployments != null)
            {
                foreach (var deployment in deployments)
                {
                    result += $"Deployment: {deployment.OperationName}, Success: {deployment.IsSuccessful}, Timestamp: {deployment.Timestamp}, Caller: {deployment.Caller}\n";
                }
            }
            else
            {
                result += "No deployment activities found.\n";
            }

            result += "\nSwap Activities:\n";

            if (swaps != null)
            {
                foreach (var swap in swaps)
                {
                    result += $"Swap: {swap.OperationName}, Success: {swap.IsSuccessful}, Timestamp: {swap.Timestamp}, Caller: {swap.Caller}\n";
                }
            }
            else
            {
                result += "No swap activities found.\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            // Handle any exceptions and return an error message  
            return $"An error occurred: {ex.Message}";
        }
    }
}
