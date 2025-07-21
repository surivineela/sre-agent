// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

/// <summary>
/// Constants related to OpenTelemetry semantic conventions for GenAI operations
/// </summary>


public static class TraceAttribute
{
    // common
    public const string ThreadId = "thread.id";

    public const string MessageContent = "message.content";

    public const string AgentName = "agent.name";

    public const string OperationName = "operation.name";
    public const string SpanPurpose = "span.purpose";

    public const string HandeOffAgentName = "handoff.agent.name";

    public const string ToolName = "tool.name";

    public const string ToolInput = "tool.input";

    public const string ToolOutput = "tool.output";

    public const string ToolDescription = "tool.description";

    public const string ModelInput = "model.input";
    public const string ModelOutput = "model.output";
    public const string ModelInputTokensCount = "model.input.tokens.count";
    public const string ModelOutputTokensCount = "model.output.tokens.count";
    public const string ModelTotalTokensCount = "model.total.tokens.count";
    public const string ModelTemperature = "model.temperature";

    // reasoning.loop
    public const string TriggeredBy = "triggered.by";
    public const string TriggeredMessage = "triggered.message";

    // user approval
    public const string ApprovalDescription = "approval.description";
    public const string ApprovalStatus = "approval.status";
}

public static class TraceOperationName
{
    // reasoning loop
    public const string ReasoningLoop = "reasoning.loop";
    public const string UserMessage = "user.message";
    public const string UserApproval = "user.approval";
    public const string UserContinueTool = "user.continue.tool";

    // agent framework
    public const string InvokeAgent = "invoke.agent";

    public const string Tool = "tool";

    public const string Handoff = "handoff";

    public const string ModelGeneration = "model.generation";

    public const string Critic = "critic";
}

