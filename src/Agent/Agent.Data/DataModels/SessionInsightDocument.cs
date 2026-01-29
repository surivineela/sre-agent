// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;

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

    // Agent Performance (Obsolete - kept for backward compatibility with existing Cosmos DB documents)
    // This property is no longer populated but must remain to allow deserialization of old documents
    AgentPerformanceMetrics? AgentPerformance,

    // Learning and Pitfalls
    List<string>? Pitfalls,
    List<string>? KeyLearnings,

    // Feedback
    List<InsightFeedback>? Feedback,

    // Raw Data
    string? TrajectoryId, // ID of the trajectory document in Agent Memory
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
    public string? TrajectoryJson { get; set; } = TrajectoryJson;
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
/// Agent performance metrics for the session.
/// This is no longer populated but kept for backward compatibility with existing Cosmos DB documents.
/// </summary>
#pragma warning disable CS0618
public record AgentPerformanceMetrics(
#pragma warning restore CS0618
    int TotalAgentTurns,
    int TotalToolCalls,
    int SuccessfulToolCalls,
    int FailedToolCalls,
    int AgentHandoffs,
    List<string>? AgentsInvolved,
    TimeSpan? TotalDuration,
    string? EfficiencyRating
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
