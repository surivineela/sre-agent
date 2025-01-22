using Azure.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.AI;
using OperationalAgentRuntime.Helpers;
using OperationalAgentRuntime.Models;
using OperationalAgentRuntime.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperationalAgentRuntime.Skills
{
    public class BasicSkills
    {
        private readonly IChatClient chatClient;

        public BasicSkills(IChatClient chatClient)
        {
            this.chatClient = chatClient;
        }


        [Function(nameof(GetSubscriptions))]
        public static async Task<List<AzureSubscription>> GetSubscriptions([ActivityTrigger] FunctionContext executionContext)
        {
            var list = await ArmHelper.GetSubscriptionsAsync();
            return list;
        }

        [Function(nameof(GetAllResources))]
        public static async Task<List<string>> GetAllResources([ActivityTrigger] string subscriptionId, FunctionContext executionContext)
        {
            var list = await ArmHelper.GetAllResourceUriAsync(subscriptionId);
            return list;
        }

        [Function(nameof(CheckBasicAuthForResources))]
        public static async Task<List<BasicAuthStatus>> CheckBasicAuthForResources([ActivityTrigger] List<string> resourceIds, FunctionContext executionContext)
        {
            var list = await ArmHelper.CheckBasicAuth(resourceIds);
            return list;
        }

        [Function(nameof(DisableBasicAuth))]
        public static async Task<bool> DisableBasicAuth([ActivityTrigger] BasicAuthStatus app, FunctionContext executionContext)
        {
            var result = await ArmHelper.DisableBasicAuth(app);
            return result;
        }

        [Function(nameof(GetAppAvailability))]
        public static async Task<List<TimeSeriesData>> GetAppAvailability([ActivityTrigger] string appResourceId, FunctionContext executionContext)
        {
            var t = new MetricsFunctionTool();
            return await t.GetAppAvailability(appResourceId);
        }

        [Function(nameof(GetAppPrivateBytes))]
        public static async Task<List<TimeSeriesData>> GetAppPrivateBytes([ActivityTrigger] string appResourceId, FunctionContext executionContext)
        {
            var metrics = new List<Metric>
                {
                    new Metric { Name = "PrivateBytes", Unit = "bytes", Aggregation = "Average" }
                };

            var metricsData = await ArmHelper.FetchMetricsAsync(appResourceId, metrics);
            var privateBytesData = metricsData
                .Where(m => m.Name == "PrivateBytes")
                .Select(m => new TimeSeriesData
                {
                    Timestamp = m.Timestamp,
                    Value = Math.Round((1.0 * m.Value / (1024.0 * 1024.0 * 1024.0)), 2), // Convert bytes to GB  
                    Unit = "GB"
                })
                .ToList();
            return privateBytesData;
        }

        [Function(nameof(GetProblemRootCause))]
        public static async Task<ApplensIssueRootCause> GetProblemRootCause([ActivityTrigger] Tuple<string,string> resourceAndProblemtStatement, FunctionContext executionContext)
        {
            return await ApplensAgentHelper.GetProblemRootCause(resourceAndProblemtStatement.Item1, resourceAndProblemtStatement.Item2);
        }

        [Function(nameof(GetAppSku))]
        public static async Task<AppPlanSku> GetAppSku([ActivityTrigger] string appResourceId, FunctionContext executionContext)
        {
            string appPlanResourceId = await ArmHelper.GetAppServicePlanNameAsync(appResourceId);
            return await ArmHelper.GetCurrentSkuAsync(appPlanResourceId);
        }

        [Function(nameof(ScaleUpAppServicePlan))]
        public static async Task<bool> ScaleUpAppServicePlan([ActivityTrigger] Tuple<string, AppPlanSku> appResourceAndTargetSku, FunctionContext executionContext)
        {
            string appPlanResourceId = await ArmHelper.GetAppServicePlanNameAsync(appResourceAndTargetSku.Item1);
            return await ArmHelper.ScaleUpAppServicePlanByNameAsync(appPlanResourceId, appResourceAndTargetSku.Item2);
        }

        [Function(nameof(CaptureMemoryDump))]
        public static async Task<string> CaptureMemoryDump([ActivityTrigger] string appResourceId, FunctionContext executionContext)
        {
            return await ArmHelper.TakeMemoryDumpAsync(appResourceId);
        }

        [Function(nameof(GetChartImageForTimeSeries))]
        public static async Task<string> GetChartImageForTimeSeries([ActivityTrigger] ChartImageInput chartImageInput, FunctionContext executionContext)
        {
            string base64Img = ChartHelper.GenerateChartBase64String(chartImageInput);
            return base64Img;
        }

        [Function(nameof(GetOpenAIResponse))]
        public async Task<string> GetOpenAIResponse([ActivityTrigger] List<ChatMessage> messages, FunctionContext executionContext)
        {            
            var res = await chatClient.CompleteAsync(messages);
            return res.Message.Text;
        }

        [Function(nameof(PostMessageToTeams))]
        public static async Task<bool> PostMessageToTeams([ActivityTrigger] TeamsMessage teamsMessage, FunctionContext executionContext)
        {
            bool result = await TeamsHelper.PostMessageAsync(teamsMessage);
            //TODO : Need to figure out a better way to preserve multiple message orderings. Putting an artificial delay for now.
            await Task.Delay(5000);
            return result;
        }

        [Function(nameof(ReadFileContent))]
        public static async Task<string> ReadFileContent([ActivityTrigger] string filePath, FunctionContext executionContext)
        {
            return await File.ReadAllTextAsync(filePath);
        }
    }
}
