// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.Reasoning;

public abstract class ReasoningLoopMessage
{
}

public class ReasoningLoopUserMessage : ReasoningLoopMessage
{
    public ChatMessage Message { get; }

    public ReasoningLoopUserMessage(ChatMessage message)
    {
        Message = message;
    }
}

public class ReasoningLoopApprovalMessage : ReasoningLoopMessage
{
    public Approval Approval { get; }

    public ReasoningLoopApprovalMessage(Approval approval)
    {
        Approval = approval;
    }
}