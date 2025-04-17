// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public record SubAgentThread(
    Guid Id,
    Guid ThreadId,
    AgentTypeEnum AgentType
);

