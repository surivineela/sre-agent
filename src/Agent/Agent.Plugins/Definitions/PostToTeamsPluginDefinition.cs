using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
