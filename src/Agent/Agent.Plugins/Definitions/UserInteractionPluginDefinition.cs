// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
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

        [Description("Sends the specified message to the user. Used this for cases where you would normally reply to the user instead of making a tool call. Do not use this if you need to wait for the user response, as this is a fire and forget notification.")]
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


