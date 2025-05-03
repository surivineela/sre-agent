// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Runtime.SubAgents.DailyReportSummary
{
    [DurableTask]
    public class DailyReportPlanActivity : TaskActivity<DailyReportSummaryInput, List<Microsoft.Extensions.AI.ChatMessage>>
    {
        private readonly IChatClient _chatClient;

        public DailyReportPlanActivity(IChatClient chatClient)
        {
            _chatClient = chatClient;
        }

        public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, DailyReportSummaryInput input)
        {
            //var resourcesDescription = string.Join(", ",
            //    input.ResourceTypesToInclude.Select(r => $"'{r}'"));

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "DailySummaryAgent", "DailySummaryPlan.txt");
            var systemPrompt = File.ReadAllText(path)
                .Replace("{{reportType}}", input.ReportType)
                .Replace("{{timespan}}", input.Timespan);

            var userMessage =
                $"Already created a daily report dashboard here: {input.ReportType.ToLower()} report summary for the following resource types:. " +
                $"Include metrics for: '{input.DashboardSummary}' over the past {input.Timespan} timespan." +
                $"Send a summary that this report has been generated and user can ask any questions on top of it";

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.System, userMessage)
            ];

            var response = await _chatClient.GetResponseAsync(messages);
            messages.Add(response.Messages[0]);

            return messages;
        }
    }
}

