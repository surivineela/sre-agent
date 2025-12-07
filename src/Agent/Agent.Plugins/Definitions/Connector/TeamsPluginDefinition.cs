// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions.Connector
{
    [AgentToolPlugin(Category = ToolCategories.Utility, EnabledIf = "DataConnectorType:Teams")]
    public class TeamsPluginDefinition
    {
        private readonly ITeamsPlugin _teamsChannelPlugin;

        public TeamsPluginDefinition(ITeamsPlugin teamsChannelPlugin)
        {
            _teamsChannelPlugin = teamsChannelPlugin;
        }

        [Description("Posts a message to the connected Microsoft Teams channel")]
        [AgentTool(ToolMode.Auto)]
        public async Task<PostMessageResult> PostTeamsMessageAsync(
            [Description("The subject of the message")]
            string subject,
            [Description("The message content to post to the Teams channel. Must be in HTML format.")]
            string message)
        {
            return await _teamsChannelPlugin.PostMessageToChannel(subject, message);
        }

        [Description("Retrieves messages from the connected Microsoft Teams channel")]
        [AgentTool(ToolMode.Auto)]
        public async Task<List<TeamsChannelMessage>> GetTeamsMessagesAsync()
        {
            return await _teamsChannelPlugin.GetMessagesFromChannel();
        }

        [Description("Replies to an existing Microsoft Teams channel message using HTML content")]
        [AgentTool(ToolMode.Auto)]
        public async Task<PostMessageResult> ReplyToTeamsMessageAsync(
            [Description("The message ID to reply to. Use GetTeamsMessages to obtain IDs before calling this tool.")]
            string messageId,
            [Description("HTML body for the reply. Plain text is not supported.")]
            string messageHtml,
            [Description("Optional subject for the reply")]
            string? subject = null)
        {
            return await _teamsChannelPlugin.ReplyToTeamsMessageAsync(messageId, messageHtml, subject);
        }
    }
}
