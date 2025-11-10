// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.Reasoning;

public class ReasoningLoopIterationResult()
{
    public bool IsContinuation { get; set; } = false;

    public List<UserActionRequiredResult> UserActionRequiredResults { get; set; } = [];

    public bool HasUserActionRequiredResults => UserActionRequiredResults.Count > 0;

    public bool AreUserActionsCompleted => UserActionRequiredResults.All(r => r.Result != null);

    public bool HasResultForCallId(string callId)
    {
        return UserActionRequiredResults.Any(r => r.CallId == callId && r.Result != null);
    }

    public FunctionResultContent? GetResultForCallId(string callId)
    {
        return UserActionRequiredResults.FirstOrDefault(r => r.CallId == callId)?.Result;
    }

    public void SetResultForCallId(string callId, FunctionResultContent result)
    {
        var userAction = UserActionRequiredResults.FirstOrDefault(r => r.CallId == callId);
        if (userAction != null)
        {
            userAction.Result = result;
        }
    }
}

public class UserActionRequiredResult
{
    public required ManualToolCall ManualToolCall { get; init; }
    public FunctionResultContent? Result { get; set; }
    public string CallId => ManualToolCall.FunctionCall.CallId;
}
