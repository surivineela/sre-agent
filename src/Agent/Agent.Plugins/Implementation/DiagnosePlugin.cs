// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json;
using Agent.Core;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Agent.Plugins
{
    public class DiagnosePlugin : IDiagnosePlugin
    {
        private static readonly AsyncOperationTracker<string, string, string> _diagnoseTracker =
            new(
                func: DiagnoseAppServiceInternal,
                funcShouldSendTeamsNotification: modelOutput => modelOutput.Contains("<unhealthy>")
            );

        public string Diagnose(Kernel kernel, IReadOnlyList<string> resourceIdList)
        {
            resourceIdList = resourceIdList.Distinct().ToArray();
            _ = DiagnoseMultipleApps(kernel, resourceIdList);
            return $"Diagnosis for ${resourceIdList.Count} apps started";
        }

        public AsyncOperationStatusSummary<string, string>? GetDiagnoseStatus(string resourceId)
        {
            return _diagnoseTracker.GetOperationSummary(resourceId);
        }

        private async Task DiagnoseMultipleApps(Kernel kernel, IReadOnlyList<string> resourceIdList)
        {
            foreach (var resourceId in resourceIdList)
            {
                _diagnoseTracker.TryStartOperation(
                    kernel,
                    contextMessage: $"Diagnose multiple apps",
                    resourceId,
                    parameter: ""
                );
            }

            await Task.WhenAll(
                resourceIdList.Select(resourceId =>
                    _diagnoseTracker.GetTask(resourceId) ?? Task.CompletedTask
                )
            );

            var jsonResult = JsonSerializer.Serialize(
                resourceIdList
                    .Select(_diagnoseTracker.GetOperationSummary)
                    .Where(s => s is not null)
                    .Select(s => new
                    {
                        OperationStatus = s.OverallStatus,
                        DiagnoseResult = s.Details,
                        ResourceId = s.Descriptor,
                    })
            );

            await ChatHistoryPersistency.ChatHistoryTransition(async history =>
            {
                history.AddSystemMessage(
                    $"Background diagnosis for multiple apps has finished, please summarize healthy app in concise words and unhealthy app in more details. Here is the result in json format: {jsonResult}"
                );

                var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
                var mainChatResult = await chatCompletionService.GetChatMessageContentAsync(
                    history,
                    executionSettings: new()
                    {
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    },
                    kernel: kernel
                );

                await GlobalStatic.TeamsConnector.PostMessageAsync(
                    new TeamsMessage(content: mainChatResult.Content ?? string.Empty)
                );
                history.AddAssistantMessage(mainChatResult.Content ?? string.Empty);

                return 0;
            });
        }

        private static async Task<string> DiagnoseAppServiceInternal(
            Kernel kernel,
            string resourceId,
            string _parameter,
            Action<string> funcReportProgress,
            // TODO: use the token to cancel operation
            CancellationToken cancellationToken
        )
        {
            var chatHistory = new ChatHistory();

            /*
            chatHistory.AddSystemMessage("You are an expert of Azure App Service diagnosing, you are now going to working on the following App Service resource" +
                $" id: {resourceId} , you should go through all the metrics of this App Service instance, and come to a conclusion that this app is healthy or unhealthy. No 200 requests do not imply app is unhealthy but simply it's getting low requests" +
                $"In the conclusion, please output info about the unhealthy resource (and explain why you think unhealthy) and give a short summary about healthy resources in 1-2 lines. Highlight " +
                $"**Display Charts for Unhealthy Resources**\\n\" +\r\n         \"   - If numeric data is provided (by category/timestamp), call the plot_time_series_data plugin.\\n\" +\r\n         \"   - Focus on charting metrics for Unhealthy resources. Skip healthy metrics charts unless requested.\\n\" +\r\n         \"   - Remember zero metrics don't indicate a failure, low request rate also doesn't indicate a failure\" +\r\n         \"   - **Always visualize**:\\r\" +\r\n         \"     - Memory leaks\\r\" +\r\n         \"     - CPU spikes\\r\" +\r\n         \"     - Error rate patterns\\r\" +\r\n         \"     - Response time degradation\\n\\n\" +" +
                "<Important> If you find metrics indicates unhealthy, call plot_time_series_data plugin to explain failure metrics to user. </Important>" +
                "<Important> If you find metrics indicates healthy, just summarize in concise word to the user, no need to breakdown by individual metrics kind you inspected, an overall summary is good enough. </Important>");
            */

            chatHistory.AddSystemMessage(
                $@"You are an Azure App Service diagnostics expert analyzing the App Service instance with resource ID: {resourceId}

Primary Responsibilities:
1. Analyze all relevant App Service metrics to assess application health
2. Provide a data-driven conclusion on whether the application is healthy or unhealthy
3. Generate visualizations for concerning metrics when issues are detected, healthy resources should be skipped

Health Assessment Guidelines:
- Low or zero request counts alone do not indicate unhealthiness, it may mean app has low usage
- Focus on performance degradation patterns and error indicators
- Key metrics to monitor:
  � Memory usage and potential leaks
  � CPU utilization patterns
  � HTTP error rates
  � Response time trends
  � Server response codes (Note: Low 200 counts alone are not concerning)

Output Format:

FOR UNHEALTHY RESOURCES:
- Provide detailed analysis of problematic metrics
- Call plot_time_series_data plugin to visualize:
  � Significant metric degradation
  � Error patterns
  � Resource exhaustion trends
  � Performance bottlenecks
- Explain specifically why you concluded the resource is unhealthy
- Include remediation suggestions when possible
- Append <unhealthy> to the end of output

FOR HEALTHY RESOURCES:
- Provide a brief 1-2 line summary confirming health status
- No detailed metric breakdown or visualizations needed unless specifically requested
- Append <healthy> to the end of output

Remember:
- Always support conclusions with metric evidence
- Focus visualizations on problematic patterns
- Maintain a solution-oriented approach when issues are found"
            );
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
            var result = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() },
                kernel: kernel
            );

            return result.Content ?? string.Empty;
        }
    }
}
