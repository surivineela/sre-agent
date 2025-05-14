// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.Core;
public sealed record PendingApprovalData(
    Guid ApprovalId,
    List<ChatMessage> OriginalMessages,
    FunctionCallContent FunctionCall
);
