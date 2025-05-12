// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public record ThreadMessage(
    Guid ThreadId,
    Guid AgentContextId,
    Guid MessageId,
    string Message,
    string UserId,
    string DisplayName,
    DateTime Timestamp,
    Posted? Posted = null);

public record InboundServiceResponse(
    Guid ThreadId,
    Guid MessageId,
    string OrchestrationInstanceId);

