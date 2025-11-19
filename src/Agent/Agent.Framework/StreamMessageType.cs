// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

/// <summary>
/// Defines the types of messages that can be streamed to client
/// when property is null, type is just pure text
/// </summary>
public enum StreamMessageType
{
    /// <summary>
    /// Chart or visualization data
    /// </summary>
    Chart,

    /// <summary>
    /// Approval workflow
    /// </summary>
    Approval,

    /// <summary>
    /// Mermaid diagram data
    /// </summary>
    Mermaid,

    /// <summary>
    /// Base64 image data
    /// </summary>
    Image,

    /// <summary>
    /// Azure Cli Execution message type.
    /// </summary>
    AzCli,

    /// <summary>
    /// Kubectl Execution message type.
    /// </summary>
    Kubectl,

    /// <summary>
    /// PostgreSQL Execution message type.
    /// </summary>
    Psql,

    /// <summary>
    /// Thread creation or update message type.
    /// </summary>
    ThreadEvent,

    /// <summary>
    /// Action update message type.
    /// </summary>
    Action,

    /// <summary>
    /// Agent task progress updates
    /// </summary>
    TaskProgress,

    /// <summary>
    /// Agent task updates (full task object)
    /// </summary>
    TaskUpdate,

    /// <summary>
    /// Change diff viewer for resource modifications
    /// </summary>
    ChangeDiff,

    /// <summary>
    /// Agent Task/Deep investigation message card
    /// </summary>
    DeepInvestigation,

    /// <summary>
    /// Todo Plan message card
    /// </summary>
    TodoPlan,

    /// <summary>
    /// Status update for an incident, re-reported when an incident appears
    /// </summary>
    IncidentStatus,

    /// <summary>
    /// Memory search results with structured data for rendering memory card
    /// </summary>
    MemorySearch,

    /// <summary>
    /// Trajectory insights and learnings posted after thread evaluation
    /// </summary>
    TrajectoryInsight,

    /// <summary>
    /// Intermediate chat updates (tool preambles) sent during agent processing
    /// </summary>
    IntermediateUpdate,

    /// <summary>
    /// Model Reasoning Summary (only returned for Responses API on reasoning models)
    /// </summary>
    Reasoning,
}
