// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.AgentTasks;

public interface IAgentTaskHandler
{
    /// <summary>
    /// Executes the agent task. This method returns a task that represents the asynchronous operation.
    /// When the returned task completes successfully, the agent task is considered to be completed.
    /// This method should be idempotent.
    /// </summary>
    /// <param name="agentTask">The agent task to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ExecuteAsync(AgentTask agentTask, CancellationToken cancellationToken);
}
