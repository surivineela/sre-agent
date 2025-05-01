// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.Core;

public sealed record ExecuteActionInput(
    Guid ThreadId,
    Guid? ApprovalId,
    FunctionCallContent FunctionCallContent,
    IReadOnlyList<string> ToolSignatures
);

