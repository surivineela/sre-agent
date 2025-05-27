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

        [Description("Sends the specified message to the user. Use this to send updates about your current task as you are working on it. Do not use this for asking questions to the user, only for status updates.")]
        public string NotifyUser(
            [Description("The message to send to the user.")]
            string message)
        {
            _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(ThreadId, string.Empty,
                new ChatMessage(ChatRole.Assistant, message));

            return "User notified";
        }
    }
}


