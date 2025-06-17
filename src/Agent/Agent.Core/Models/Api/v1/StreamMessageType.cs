// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

/// <summary>
/// Defines the types of messages that can be streamed to client
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
    Approval
}
