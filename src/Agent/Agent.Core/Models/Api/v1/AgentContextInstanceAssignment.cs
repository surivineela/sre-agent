// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public record AgentContextInstanceAssignment(
    string AgentContextId,
    string ThreadId,
    string InstanceId,
    DateTimeOffset Expires
);
