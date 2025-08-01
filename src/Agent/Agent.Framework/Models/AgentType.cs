// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework.Models;

/// <summary>
/// Defines the execution type of an agent.
/// </summary>
public enum AgentType
{
    /// <summary>
    /// Autonomous agent that operates independently with conversation history.
    /// </summary>
    Autonomous,
    
    /// <summary>
    /// Orchestrator agent that coordinates workflow execution.
    /// </summary>
    Orchestrator,
    
    /// <summary>
    /// Activity agent that performs specific tasks within a workflow.
    /// </summary>
    Activity
}
