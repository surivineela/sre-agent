// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Action = Agent.Core.Models.Api.v1.Action;

namespace Agent.Plugins.Definitions
{
    /// <summary>
    /// Interface for plugins that record important agent actions
    /// </summary>
    public interface IRecordActionsPlugin
    {
        /// <summary>
        /// Gets details about a specific action
        /// </summary>
        /// <param name="threadId">The thread ID this action is associated with</param>
        /// <param name="actionId">The ID of the action to retrieve</param>
        /// <returns>The action details</returns>
        Task<Action> GetAction(Guid threadId, Guid actionId);
    }
}

