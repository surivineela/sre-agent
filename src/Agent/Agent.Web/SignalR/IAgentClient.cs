// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

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
        /// Sends an incident update to the client
        /// </summary>
        Task IncidentUpdate(ChatResponseUpdate update);

        /// <summary>
        /// Sends a todo plan update to the client for timeline display
        /// </summary>
        Task TodoPlanUpdate(ChatResponseUpdate update);

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

        /// <summary>
        /// Sends a Task tool (subagent) execution update to the client
        /// </summary>
        Task SubagentUpdate(ChatResponseUpdate update);
    }
}
