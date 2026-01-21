// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

/// <summary>
/// Session insight information exposed via API - filtered to exclude sensitive trajectory data
/// </summary>
public record SessionInsight(
    string Id, // Insight ID - used to uniquely identify this specific insight
    string ThreadId,
    string Title,
    DateTime GeneratedTimestamp,

    // Only the markdown insight message - no raw trajectory data
    string? InsightMarkdown,

    // Feedback
    List<SessionInsightFeedback>? Feedback,

    // Statistics
    int FeedbackCount,
    int PositiveFeedbackCount,
    int NegativeFeedbackCount
);

public record SessionInsightFeedback(
    string FeedbackId,
    DateTime SubmittedAt,
    string? Rating,
    string? Comment,
    bool? HasExtractedKnowledge = null // True if knowledge was extracted from feedback
);

/// <summary>
/// Request model for submitting feedback to a session insight
/// </summary>
public record SessionInsightFeedbackRequest(
    string? InsightId,
    string? Rating,
    string? Comment
);

/// <summary>
/// List of session insights with pagination info
/// </summary>
public record SessionInsightList(
    List<SessionInsight> Insights,
    int TotalCount,
    int Skip,
    int Take,
    bool HasMore
);

/// <summary>
/// Summary view of a session insight for list displays - no sensitive data
/// </summary>
public record SessionInsightSummary(
    string ThreadId,
    string Title,
    DateTime GeneratedTimestamp,
    int FeedbackCount
);
