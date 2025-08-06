// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;

namespace Agent.Web.SignalR
{
    /// <summary>
    /// Strongly typed interface for AgentHub client methods
    /// </summary>
    public interface IAgentClient
    {
        /// <summary>
        /// Sends a thread update to the client
        /// </summary>
        Task ThreadUpdate(ChatResponseUpdate update);

        /// <summary>
        /// Sends a thread update to the client
        /// </summary>
        Task ActionUpdate(ChatResponseUpdate update);

        /// <summary>
        /// Sends a message update to the client
        /// </summary>
        Task MessageUpdate(ChatResponseUpdate update);

        /// <summary>
        /// Sends a task update to the client
        /// </summary>
        Task TaskUpdate(ChatResponseUpdate update);

        /// <summary>
        /// Sends a text-only update to the client
        /// </summary>
        Task TextUpdate(string text);

        /// <summary>
        /// Sends an error message to the client
        /// </summary>
        Task Error(ChatResponseUpdate errorMessage);

        /// <summary>
        /// Responds to a ping with the current timestamp
        /// </summary>
        Task Pong(DateTime timestamp);
    }
}
