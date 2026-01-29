// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

/// <summary>
/// Represents the response DTO for thread evaluation results (excludes SATScore, PriorityReason, and UserInteractionCount)
/// </summary>
public record ThreadEvaluateResultResponse(
    Guid Id,
    Guid ThreadId,
    string ThreadTitle,
    TimeSpan Duration,
    int ToolCallCount,
    double ToolCallSuccessRate,
    int AzCliCallCount,
    double AzCliSuccessRate,
    int KubectlCallCount,
    double KubectlSuccessRate,
    string EvaluationSummary,
    string Category,
    int Resolved,
    int Satisfied,
    int Automatic,
    int Smooth,
    int Concise,
    int Adherence,
    string Priority,
    DateTime EvaluatedTimestamp,
    AgentTypeEnum AgentType,
    string StartingAgentName,
    bool SkillsEnabled,
    bool IsExtendedAgent,
    string BlockedReason = "None"
);

/// <summary>
/// Extension methods for converting ThreadEvaluateResult to ThreadEvaluateResultResponse
/// </summary>
public static class ThreadEvaluateResultExtensions
{
    /// <summary>
    /// Converts a ThreadEvaluateResult to ThreadEvaluateResultResponse (excludes SATScore, PriorityReason, and UserInteractionCount)
    /// </summary>
    public static ThreadEvaluateResultResponse ToResponse(this ThreadEvaluateResult result)
    {
        return new ThreadEvaluateResultResponse(
            Id: result.Id,
            ThreadId: result.ThreadId,
            ThreadTitle: result.ThreadTitle,
            Duration: result.Duration,
            ToolCallCount: result.ToolCallCount,
            ToolCallSuccessRate: result.ToolCallSuccessRate,
            AzCliCallCount: result.AzCliCallCount,
            AzCliSuccessRate: result.AzCliSuccessRate,
            KubectlCallCount: result.KubectlCallCount,
            KubectlSuccessRate: result.KubectlSuccessRate,
            EvaluationSummary: result.EvaluationSummary,
            Category: result.Category,
            Resolved: result.Resolved,
            Satisfied: result.Satisfied,
            Automatic: result.Automatic,
            Smooth: result.Smooth,
            Concise: result.Concise,
            Adherence: result.Adherence,
            Priority: result.Priority,
            EvaluatedTimestamp: result.EvaluatedTimestamp,
            AgentType: result.AgentType,
            StartingAgentName: result.StartingAgentName,
            SkillsEnabled: result.SkillsEnabled,
            IsExtendedAgent: result.IsExtendedAgent,
            BlockedReason: result.BlockedReason
        );
    }

    /// <summary>
    /// Converts an enumerable of ThreadEvaluateResult to ThreadEvaluateResultResponse
    /// </summary>
    public static IEnumerable<ThreadEvaluateResultResponse> ToResponse(this IEnumerable<ThreadEvaluateResult> results)
    {
        return results.Select(r => r.ToResponse());
    }
}
