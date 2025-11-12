// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Core.Interfaces;
using Agent.Plugins.Interface;
using Microsoft.Extensions.AI;

namespace Agent.Plugins.AgentInteraction
{
    public class AgentInteractionPlugin : IAgentInteractionPlugin
    {
        private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;

        public AgentInteractionPlugin(IAgentOutboundCommunicationService agentOutboundCommunicationService)
        {
            _agentOutboundCommunicationService = agentOutboundCommunicationService;
        }

        public async Task<string> ShareAgentResultAsync(string calledAgentName, string analysisSummary, string? context = null, int resultSummaryLimit = 4096)
        {

            // If the result is too long, summarize it
            var displayResult = analysisSummary.Length > resultSummaryLimit
                ? $"{analysisSummary.Substring(0, resultSummaryLimit)}... (truncated)"
                : analysisSummary;

            // Create the chat message
            var messageContent = FormatAgentResultMessage(calledAgentName, displayResult, context);

            var msg = new ChatMessage(ChatRole.Tool, messageContent);

            // Update the thread with the message
            await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                ToolStatic.AsyncLocalThreadId.Value,
                string.Empty,
                msg);

            return $"Agent interaction result from '{calledAgentName}' has been shared successfully.";
        }

        private static string FormatAgentResultMessage(string calledAgentName, string result, string? context)
        {
            var message = $"## Agent Interaction Result{Environment.NewLine}{Environment.NewLine}";
            message += $"**Called Agent:** `{calledAgentName}`{Environment.NewLine}{Environment.NewLine}";

            if (!string.IsNullOrEmpty(context))
            {
                message += $"**Context:** {context}{Environment.NewLine}{Environment.NewLine}";
            }

            message += $"**Result:**{Environment.NewLine}```{Environment.NewLine}{result}{Environment.NewLine}```";

            return message;
        }
    }
}
