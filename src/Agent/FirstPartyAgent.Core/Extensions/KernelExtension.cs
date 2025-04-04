// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Extensions
{
    public static class KernelExtensions
    {
        public static async Task LogInformation(this Kernel kernel, string info, ILogger logger, ITeamsClient teamsClient = null)
        {
            logger.LogInformation(info);
            if (teamsClient != null && teamsClient.IsEnabled() && teamsClient.SendLogsToTeams())
            {
                string agentMode = kernel.Data.TryGetValue("agentMode", out var val) ? val.ToString() : AgentMode.None.ToString();
                await teamsClient.PostMessageOnTeams(info, agentMode, null).ConfigureAwait(false);
            }
        }
    }
}

