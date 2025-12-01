// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using OpenTelemetry.Trace;

namespace Agent.Logging;

/// <summary>
/// Extension methods for the Tracer class to provide common tracing utilities.
/// </summary>
public static class TracerExtensions
{
    /// <summary>
    /// Creates and records an error telemetry span with standard attributes.
    /// </summary>
    /// <param name="tracer">The tracer instance.</param>
    /// <param name="ex">The exception to record.</param>
    /// <param name="errorMessage">Custom error message to record in the span.</param>
    /// <param name="threadId">The thread ID associated with the error.</param>
    /// <param name="parentSpan">The parent span for the error span.</param>
    public static void RecordErrorSpan(this Tracer tracer, Exception ex, string errorMessage, string threadId, TelemetrySpan? parentSpan)
    {
        var errorSpan = tracer.StartActiveSpan("error", SpanKind.Internal, parentSpan);
        errorSpan.SetAttribute(TraceAttribute.ThreadId, threadId);
        errorSpan.SetAttribute(TraceAttribute.OperationName, "error");
        errorSpan.SetAttribute("error.message", errorMessage);
        errorSpan.SetAttribute("error.stacktrace", ex.StackTrace);
        errorSpan.End();
    }

    /// <summary>
    /// Records a user message span with standard attributes.
    /// Also sets triggered attributes on the parent span.
    /// </summary>
    /// <param name="tracer">The tracer instance.</param>
    /// <param name="threadId">The thread ID associated with the message.</param>
    /// <param name="messageContent">The content of the user message.</param>
    /// <param name="parentSpan">The parent span for the message span.</param>
    public static void RecordUserMessageSpan(this Tracer tracer, string threadId, string messageContent, TelemetrySpan? parentSpan)
    {
        parentSpan.SetTriggeredAttributes(TraceOperationName.UserMessage, messageContent);

        var msgSpan = tracer.StartActiveSpan(TraceOperationName.UserMessage, SpanKind.Internal, parentSpan);
        msgSpan.SetAttribute(TraceAttribute.ThreadId, threadId);
        msgSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.UserMessage);
        msgSpan.SetAttribute(TraceAttribute.MessageContent, messageContent);
        msgSpan.End();
    }

    /// <summary>
    /// Records a user approval span with standard attributes.
    /// Also sets triggered attributes on the parent span.
    /// </summary>
    /// <param name="tracer">The tracer instance.</param>
    /// <param name="threadId">The thread ID associated with the approval.</param>
    /// <param name="approvalDescription">The description of the approval.</param>
    /// <param name="approvalStatus">The status of the approval.</param>
    /// <param name="parentSpan">The parent span for the approval span.</param>
    public static void RecordUserApprovalSpan(this Tracer tracer, string threadId, string approvalDescription, string approvalStatus, TelemetrySpan? parentSpan)
    {
        parentSpan.SetTriggeredAttributes(TraceOperationName.UserApproval, approvalDescription);

        var msgSpan = tracer.StartActiveSpan(TraceOperationName.UserApproval, SpanKind.Internal, parentSpan);
        msgSpan.SetAttribute(TraceAttribute.ThreadId, threadId);
        msgSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.UserApproval);
        msgSpan.SetAttribute(TraceAttribute.ApprovalDescription, approvalDescription);
        msgSpan.SetAttribute(TraceAttribute.ApprovalStatus, approvalStatus);
        msgSpan.End();
    }

    /// <summary>
    /// Records a user continue tool span with standard attributes.
    /// Also sets triggered attributes on the parent span.
    /// </summary>
    /// <param name="tracer">The tracer instance.</param>
    /// <param name="threadId">The thread ID associated with the tool continuation.</param>
    /// <param name="toolName">The name of the tool (optional).</param>
    /// <param name="toolInput">The input to the tool (optional).</param>
    /// <param name="toolOutput">The output from the tool (optional).</param>
    /// <param name="parentSpan">The parent span for the tool continuation span.</param>
    public static void RecordUserContinueToolSpan(this Tracer tracer, string threadId, string? toolName, string? toolInput, string? toolOutput, TelemetrySpan? parentSpan)
    {
        parentSpan.SetTriggeredAttributes(TraceOperationName.UserContinueTool, toolName ?? string.Empty);

        var msgSpan = tracer.StartActiveSpan(TraceOperationName.UserContinueTool, SpanKind.Internal, parentSpan);
        msgSpan.SetAttribute(TraceAttribute.ThreadId, threadId);
        msgSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.UserContinueTool);

        if (!string.IsNullOrEmpty(toolName))
        {
            msgSpan.SetAttribute(TraceAttribute.ToolName, toolName);
        }

        if (!string.IsNullOrEmpty(toolInput))
        {
            msgSpan.SetAttribute(TraceAttribute.ToolInput, toolInput);
        }

        if (!string.IsNullOrEmpty(toolOutput))
        {
            msgSpan.SetAttribute(TraceAttribute.ToolOutput, toolOutput);
        }

        msgSpan.End();
    }

    /// <summary>
    /// Starts a root reasoning loop span with standard attributes.
    /// </summary>
    /// <param name="tracer">The tracer instance.</param>
    /// <param name="threadId">The thread ID associated with the reasoning loop.</param>
    /// <param name="featureConfig">Serialized feature configuration.</param>
    /// <param name="experimentVariants">Formatted experiment variants.</param>
    /// <returns>The created root span.</returns>
    public static TelemetrySpan StartReasoningLoopRootSpan(this Tracer tracer, string threadId, string featureConfig, string experimentVariants)
    {
        var rootSpan = tracer.StartRootSpan(TraceOperationName.ReasoningLoop);
        rootSpan.SetAttribute(TraceAttribute.ThreadId, threadId);
        rootSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.ReasoningLoop);
        rootSpan.SetAttribute(TraceAttribute.FeatureConfig, featureConfig);
        rootSpan.SetAttribute(TraceAttribute.ExperimentVariants, experimentVariants);
        return rootSpan;
    }

    /// <summary>
    /// Sets the triggered attributes on a span.
    /// </summary>
    /// <param name="span">The span to set attributes on.</param>
    /// <param name="triggeredBy">The operation that triggered this span.</param>
    /// <param name="triggeredMessage">The message that triggered this span.</param>
    public static void SetTriggeredAttributes(this TelemetrySpan? span, string triggeredBy, string triggeredMessage)
    {
        if (span == null) return;

        span.SetAttribute(TraceAttribute.TriggeredBy, triggeredBy);
        span.SetAttribute(TraceAttribute.TriggeredMessage, triggeredMessage);
    }

    /// <summary>
    /// Safely ends a span and sets it to null.
    /// </summary>
    /// <param name="span">The span to end.</param>
    public static void EndAndClear(ref TelemetrySpan? span)
    {
        span?.End();
        span = null;
    }
}
