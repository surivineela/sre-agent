// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models.Api.v1;

namespace Agent.Plugins.Definitions
{
    public class ControlFlowV2PluginDefinition
    {
        private readonly IControlFlowV2Plugin _controlFlowV2Plugin;

        public ControlFlowV2PluginDefinition(IControlFlowV2Plugin controlFlowV2Plugin)
        {
            _controlFlowV2Plugin = controlFlowV2Plugin ?? throw new ArgumentNullException(nameof(controlFlowV2Plugin));
        }

        [Description("Starts a wait state")]
        public async Task StartWait(
            [Description("Reason for waiting")] string waitReason,
            [Description("Optional amount of time to wait for")] TimeSpan? waitFor = null)
        {
            await _controlFlowV2Plugin.StartWait(waitReason, waitFor);
        }

        [Description("Gets the current wait state if one exists")]
        public async Task<WaitInformation?> GetWaitState()
        {
            return await _controlFlowV2Plugin.GetWaitState();
        }

        [Description("Cancels the current wait state if one exists")]
        public async Task CancelWait()
        {
            await _controlFlowV2Plugin.CancelWait();
        }

        [Description("Completes the current agent context as it has reached a concluding state.")]
        public async Task Complete()
        {
            await _controlFlowV2Plugin.Complete();
        }
    }
}


