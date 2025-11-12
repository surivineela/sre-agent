// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels;

/// <summary>
/// Represents a session insight document stored in Cosmos DB.
/// Contains structured analysis and trajectory data extracted from chat sessions.
/// </summary>
public record SessionInsightDocument(
    string Id, // Unique insight ID (Guid)
    string ThreadId,
    string Title,
    DateTime GeneratedTimestamp,
    DateTime ThreadCreatedTimestamp,
    DateTime ThreadModifiedTimestamp,
    string? ThreadSource,
    bool IsInvestigationThread,
    string? ClassificationReason,

    // Investigation Context
    string? InitialSymptoms,
    string? SymptomsObserved,
    string? RootCause,
    string? SystemDesignKnowledge,

    // Timeline and Steps
    List<TimelineItem>? Timeline,
    string? StepsFollowed,

    // Resources and Metadata
    List<string>? ResourcesInvolved,
    List<string>? ResourceTypesInvolved,
    List<string>? SubscriptionsInvolved,

    // Agent Performance
    AgentPerformanceMetrics? AgentPerformance,

    // Learning and Pitfalls
    List<string>? Pitfalls,
    List<string>? KeyLearnings,

    // Feedback
    List<InsightFeedback>? Feedback,

    // Raw Data
    string? TrajectoryJson,
    string? InsightMarkdown
) : ICosmosDocument
{
    public string DocumentType => "SessionInsight";
    public string PartitionKey => Id; // Use unique ID as partition key
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    // Mutable properties for updates
    public List<InsightFeedback>? Feedback { get; set; } = Feedback;
    public DateTime GeneratedTimestamp { get; set; } = GeneratedTimestamp;
}

/// <summary>
/// Represents a timeline item in the investigation process
/// </summary>
public record TimelineItem(
    string Title,
    string Status, // Initial, Progress, Success, Issue, Resolved, etc.
    string Description,
    int Order
);

/// <summary>
/// Agent performance metrics for the session
/// </summary>
public record AgentPerformanceMetrics(
    int TotalAgentTurns,
    int TotalToolCalls,
    int SuccessfulToolCalls,
    int FailedToolCalls,
    int AgentHandoffs,
    List<string>? AgentsInvolved,
    TimeSpan? TotalDuration,
    string? EfficiencyRating // Excellent, Good, Fair, Poor
);

/// <summary>
/// User feedback on session insights
/// </summary>
public record InsightFeedback(
    string FeedbackId,
    DateTime SubmittedAt,
    string? Rating, // positive, negative
    string? Comment,
    string? UserId
);

/// <summary>
/// Request to create or update session insight feedback
/// </summary>
public record SubmitInsightFeedbackRequest(
    string? Rating, // positive, negative  
    string? Comment
);

/// <summary>
/// Result of insight generation
/// </summary>
public record SessionInsightGenerationResult(
    bool Success,
    string? InsightId = null,
    string? Message = null,
    SessionInsightDocument? Insight = null
);
