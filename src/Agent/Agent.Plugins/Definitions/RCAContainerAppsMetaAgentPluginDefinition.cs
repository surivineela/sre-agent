// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core;
using Agent.Core.Interfaces;
using FirstPartyAgent.Common.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppsMetaAgentPluginDefinition
    {
        private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;
        private readonly IWebHostEnvironment _env;
        private readonly GeneralSettings _generalSettings;

        public RCAContainerAppsMetaAgentPluginDefinition(IAgentOutboundCommunicationService agentOutboundCommunicationService, IWebHostEnvironment env, IOptions<GeneralSettings> generalSettings)
        {
            _agentOutboundCommunicationService = agentOutboundCommunicationService;
            _env = env;
            _generalSettings = generalSettings.Value ?? new GeneralSettings();
        }

        [Description(@"
        Purpose:
        Generates a Agent Web Portal Thread Link for the current conversation in the Agent Azure Portal.

        Scenario:
        Use this tool to provide a Agent Web Portal Thread Link for the current conversation.

        Output:
        Successfully genenrated link result.
        ")]
        public async Task<string> GetAgentWebPortalThreadLink()
        {
            var templateUrl = _generalSettings.PortalThreadIdLink;
            if (_env.IsDevelopment())
            {
                templateUrl = templateUrl ?? (Environment.GetEnvironmentVariable("AGENT_ENDPOINT") + "/static/#/views/activities/threads/{0}");
            }
            var currentThreadId = ToolStatic.AsyncLocalThreadId.Value;
            var threadLink = string.Format(templateUrl, currentThreadId);
            var msg = new ChatMessage(ChatRole.System, // setting system role to avoid LLM sending further information about this message
            [
                new UriContent(threadLink, "text/html"),
                new TextContent($"You can view this conversation in a more compact and user-friendly format using the [Azure portal thread link]({threadLink}).{Environment.NewLine + Environment.NewLine}**Thread ID:** {currentThreadId}")
            ]);
            await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(currentThreadId, string.Empty, msg);
            return "Thread link updated in chat successfully.";
        }
    }
}
