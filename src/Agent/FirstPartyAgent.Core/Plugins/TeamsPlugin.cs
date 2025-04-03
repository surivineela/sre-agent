// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Logging;
using FirstPartyAgent.Core.Services;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using FirstPartyAgent.Models;

namespace FirstPartyAgent.Core.Plugins
{
    public class TeamsPlugin
    {
        private readonly ITeamsClient _teamsClient;
        private readonly ILogger<TeamsPlugin> _logger;
        public TeamsPlugin(ITeamsClient teamsClient, ILogger<TeamsPlugin> logger)
        {
            _teamsClient = teamsClient;
            _logger = logger;
        }

        [KernelFunction("send_status_message")]
        [Description("Send status message about the next steps being taken")]
        public async Task<string> SendStatusMessage(
            [Description("Incident ID")] string incidentId,
            [Description("Status Message")] string statusMessage,
            Kernel kernel)
        {
            _logger.LogInformation($"[send_status_message][{DateTime.UtcNow}] Invoked with incidentId {incidentId}, statusMessage: {statusMessage}");
            if (_teamsClient.IsEnabled())
            {
                string agentMode = kernel.Data.TryGetValue("agentMode", out var val) ? val.ToString() : AgentMode.None.ToString();
                await _teamsClient.PostMessageOnTeams(statusMessage, agentMode);
                return "Sent status message on Teams";
            }
            return "Success";
        }
    }
}

