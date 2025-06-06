// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Framework;

public class SingleStepResult<TContext> where TContext : class
{
    public required ChatResponse ModelResponse { get; set; }
    public required List<ChatMessage> OriginalInput { get; set; }
    public List<ChatMessage> PreStepItems { get; set; } = [];
    public List<ChatMessage> NewStepItems { get; set; } = [];
    public required NextStep<TContext> NextStep { get; set; }

    public List<ChatMessage> GeneratedItems => [.. PreStepItems, .. NewStepItems];
}

public enum NextStepType
{
    Handoff, // handoff to another agent
    FinalOutput, // final output
    RunAgain, // run the agent again (tool calls were made)
    ManualTool, // caller must run a manual tool
}

public class NextStep<TContext> where TContext : class
{
    public required NextStepType Type { get; set; }
    public Agent<TContext>? Agent { get; set; }
    public ManualToolCall? ManualToolCall { get; set; }

    public object? Output { get; set; }
}
