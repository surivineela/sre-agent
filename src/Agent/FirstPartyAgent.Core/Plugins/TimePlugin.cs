// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FirstPartyAgent.Core.Plugins
{
    public class TimePlugin
    {
        private readonly ILogger<TimePlugin> _logger;
        private readonly ITeamsClient _teamsClient;
        public TimePlugin(ILogger<TimePlugin> logger, ITeamsClient teamsClient)
        {
            _logger = logger;
            _teamsClient = teamsClient;
        }

        [KernelFunction("wait_timer")]
        [Description("Wait for a specified number of seconds. This is useful for pacing the execution of tasks.")]
        public async Task WaitTimer([Description("Wait time in seconds")] int waitTimeInSeconds, Kernel kernel)
        {
            try
            {
                var logMessage = $"[wait_timer][{DateTime.UtcNow}] Invoked with waitTimeInSeconds {waitTimeInSeconds}";
                await kernel.LogInformation(logMessage, _logger, _teamsClient);
                await Task.Delay(waitTimeInSeconds * 1000);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while waiting: {ex.Message}");
                throw new Exception($"An error occurred while waiting: {ex.Message}");
            }
        }
    }
}

