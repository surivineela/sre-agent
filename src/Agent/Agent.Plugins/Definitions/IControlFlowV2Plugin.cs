// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Plugins.Definitions
{
    /// <summary>
    /// Interface for managing wait states in agent threads
    /// </summary>
    public interface IControlFlowV2Plugin
    {
        /// <summary>
        /// Starts a wait state for the thread
        /// </summary>
        /// <param name="waitReason">Reason for waiting</param>
        /// <param name="waitUntil">Optional amount of time to wait for</param>
        Task StartWait(string waitReason, TimeSpan? waitFor = null);

        /// <summary>
        /// Gets the current wait state for the thread if it exists
        /// </summary>
        /// <returns>The wait state if the thread is waiting, null otherwise</returns>
        Task<WaitInformation?> GetWaitState();

        /// <summary>
        /// Cancels the current wait state if one exists
        /// </summary>
        Task CancelWait();

        /// <summary>
        /// Completes the current agent context as it has reached a concluding state.
        /// </summary>
        Task Complete();
    }
}
