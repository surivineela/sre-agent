// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.Reasoning;

public abstract class ReasoningLoopMessage
{
}

public class ReasoningLoopChatMessage : ReasoningLoopMessage
{
    public ChatMessage Message { get; }

    public ReasoningLoopChatMessage(ChatMessage message)
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

public class ReasoningLoopFunctionCall : ReasoningLoopMessage
{
    public List<ChatMessage> Messages { get; }

    public ReasoningLoopFunctionCall(List<ChatMessage> messages)
    {
        Messages = messages;
    }
}

public class ReasoningLoopContinuation : ReasoningLoopMessage
{
}
