// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

/// <summary>
/// Represents the result of evaluating a thread's behavior and performance
/// </summary>
public record ThreadEvaluateResult(
    Guid Id,
    Guid ThreadId,
    string ThreadTitle,
    TimeSpan Duration,
    int UserInteractionCount,
    int ToolCallCount,
    double ToolCallSuccessRate,
    int AzCliCallCount,
    double AzCliSuccessRate,
    int KubectlCallCount,
    double KubectlSuccessRate,
    string EvaluationSummary,

    double SATScore,
    string Category,
    int Resolved,
    int Satisfied,
    int Automatic,
    int Smooth,
    int Concise,
    string Priority,
    string PriorityReason,
    DateTime EvaluatedTimestamp
)
{

}
