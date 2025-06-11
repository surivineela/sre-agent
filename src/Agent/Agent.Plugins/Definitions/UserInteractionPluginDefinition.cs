// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Interfaces;
using Agent.Framework;
using Microsoft.Extensions.AI;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin]
    public class UserInteractionPluginDefinition
    {
        public Guid? ThreadId { get; set; }
        private readonly IAgentOutboundCommunicationService _outboundCommunicationService;

        public UserInteractionPluginDefinition(IAgentOutboundCommunicationService outboundCommunicationService)
        {
            _outboundCommunicationService = outboundCommunicationService;
        }

        [AgentTool(ToolMode.Auto)]
        [Description("Sends the specified message to the user. Use this to send updates about your current task as you are working on it. Do not use this for asking questions to the user, only for status updates.")]
        public string NotifyUser(
            [Description("The message to send to the user.")]
            string message)
        {
            _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(ThreadId, string.Empty,
                new ChatMessage(ChatRole.Assistant, message));

            return "User notified";
        }

        [AgentTool(ToolMode.Auto)]
        [Description("Sends the specified message to the user and indicates that you require a response to proceed. Do not use this for any scenario where you just need to send the user an update in a fire and forget manner. If the user responds in a manner that does not satisfactorily answer your question, use this tool again.")]
        public string AskUserForInput(
            [Description("The message to send to the user.")]
            string message)
        {
            _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(ThreadId, string.Empty,
                new ChatMessage(ChatRole.Assistant, message));

            return "Question sent to user. The agent will wait for a response before proceeding with further actions.";

        }
    }
}


