// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

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
}
