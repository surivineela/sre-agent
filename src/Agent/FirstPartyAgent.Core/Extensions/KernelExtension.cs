// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Extensions
{
    public static class KernelExtensions
    {
        public static async Task LogInformation(this Kernel kernel, string info, ILogger logger, ITeamsClient teamsClient = null, ISessionMessageService sessionMessageService = null)
        {
            logger.LogInformation(info);
            if (teamsClient != null && teamsClient.IsEnabled() && teamsClient.SendLogsToTeams())
            {
                string agentMode = kernel.Data.TryGetValue("agentMode", out var val) ? val.ToString() : AgentMode.None.ToString();
                var teamsMessage = new TeamsMessage(info, null);
                await teamsClient.PostMessageOnTeams(agentMode, teamsMessage).ConfigureAwait(false);
            }

            if (sessionMessageService != null)
            {
                if(kernel.Data.ContainsKey("sessionId"))
                {
                    string sessionId = (string)kernel.Data["sessionId"];
                    var publisher = sessionMessageService.GetPublisher(sessionId);
                    if(publisher != null)
                    {
                        await publisher(info).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}

