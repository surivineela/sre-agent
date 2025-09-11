// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using Agent.Core.Models;
using OpenTelemetry.Trace;

namespace Agent.Core;

/// <summary>
/// This class holds thread local variables used for calling tools.
/// They must be thread local because durable tasks are executed in different threads.
/// </summary>
public static class ToolStatic
{
    /// <summary>
    /// Holds the thread ID for the current Durable task. Set by GenericExecuteActionActivity
    /// AsyncLocal because we want to keep the conversation thread ID for the current async context.
    /// </summary>
    public static readonly AsyncLocal<Guid> AsyncLocalThreadId = new();

    /// <summary>
    /// Holds the approval context for the current Durable task. Set by GenericExecuteActionActivity
    /// AsyncLocal because we want to keep the conversation thread ID for the current async context.
    /// </summary>
    public static readonly AsyncLocal<ApprovalContext> AsyncLocalApprovalContext = new();

    /// <summary>
    /// Holds the cancellation token for the current operation. Set by reasoning loop during tool execution.
    /// AsyncLocal because we want to keep the cancellation token for the current async context.
    /// </summary>
    public static readonly AsyncLocal<CancellationToken> AsyncLocalCancellationToken = new();

    public static readonly AsyncLocal<TelemetrySpan?> AsyncLocalToolTraceSpan = new();

    /// <summary>
    /// Holds the current agent task ID when executing within deep investigation context.
    /// Set by IncidentInvestigationTaskHandler during tool execution.
    /// </summary>
    public static readonly AsyncLocal<Guid?> AsyncLocalAgentTaskId = new();

    /// <summary>
    /// Holds the current investigation step context during agent task execution.
    /// Set by IncidentInvestigationTaskHandler to track which investigation phase is executing.
    /// </summary>
    public static readonly AsyncLocal<InvestigationStepContext?> AsyncLocalInvestigationStepContext = new();
}

/// <summary>
/// Context information about the current investigation step being executed.
/// </summary>
public record InvestigationStepContext(
    string StepPhase, // "InitialInvestigation", "HypothesisValidation"
    string? StepName = null,
    Guid? HypothesisId = null
);
