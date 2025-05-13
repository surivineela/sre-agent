// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using FirstPartyAgent.Core.Plugins.Interfaces;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    // These are tools exposed to any-sub agent that uses this plugin but mostly it will be used by 'HelloWorldAgent'
    // Note!!: If this plugin is used by other agent, then we are mixing the concerns and we need to refactor this plugin
    public class HelloWorldPluginDefinition
    {
        private readonly IHelloWorldPlugin _plugin;

        public HelloWorldPluginDefinition(IHelloWorldPlugin Plugin)
        {
            _plugin = Plugin;
        }

        [Description("Returns a hello world message when some one send a just plain 'hello' message. DO NOT USE this tool for any other message")]
        public Task<string> GetHelloWorldMessageAsync()
        {
            return _plugin.GetHelloWorldMessageAsync();
        }

    }
}

