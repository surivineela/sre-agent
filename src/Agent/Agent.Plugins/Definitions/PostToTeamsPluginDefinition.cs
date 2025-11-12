// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Plugins.Interface;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    public class PostToTeamsPluginDefinition
    {

        private readonly IPostToTeamsPlugin _postToTeamsPlugin;

        public PostToTeamsPluginDefinition(IPostToTeamsPlugin postToTeamsPlugin)
        {
            _postToTeamsPlugin = postToTeamsPlugin;
        }

        [KernelFunction("post_message_to_user_teams")]
        [Description("Used to communicate with the User. This is the end user for the SRE Agent, Message should address them as such")]
        public async Task<string> PostMessage(string message)
        {
            return await _postToTeamsPlugin.PostAsync(message);
        }
    }
}

