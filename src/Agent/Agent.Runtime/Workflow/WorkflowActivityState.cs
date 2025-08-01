// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Runtime.Workflow;

/// <summary>
/// Defines the execution state of a workflow activity agent.
/// </summary>
public enum WorkflowActivityState
{
    /// <summary>
    /// Agent is still processing the assigned task.
    /// </summary>
    Processing,

    /// <summary>
    /// Agent has completed its task successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Agent failed to complete its task.
    /// </summary>
    Failed,

    /// <summary>
    /// Agent requires additional input to complete its task.
    /// </summary>
    RequiresInput
}
