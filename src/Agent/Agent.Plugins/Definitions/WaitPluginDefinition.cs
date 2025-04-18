// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models.Api.v1;

namespace Agent.Plugins.Definitions
{
    public class WaitPluginDefinition
    {
        private readonly IWaitPlugin _waitPlugin;

        public WaitPluginDefinition(IWaitPlugin waitPlugin)
        {
            _waitPlugin = waitPlugin ?? throw new ArgumentNullException(nameof(waitPlugin));
        }

        [Description("Starts a wait state")]
        public async Task StartWait(
            [Description("Reason for waiting")] string waitReason,
            [Description("Optional time to wait until")] DateTime? waitUntil = null)
        {
            await _waitPlugin.StartWait(waitReason, waitUntil);
        }

        [Description("Gets the current wait state if one exists")]
        public async Task<WaitInformation?> GetWaitState()
        {
            return await _waitPlugin.GetWaitState();
        }

        [Description("Cancels the current wait state if one exists")]
        public async Task CancelWait()
        {
            await _waitPlugin.CancelWait();
        }
    }
}

