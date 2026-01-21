// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models;

/// <summary>
/// User feedback on session insights.
/// This is stored in SessionInsight documents.
/// Knowledge is extracted from feedback and indexed to trajectories.
/// </summary>
public record InsightFeedback(
    string FeedbackId,
    DateTime SubmittedAt,
    string? Rating, // positive, negative
    string? Comment,
    string? UserId,
    ExtractedFeedbackKnowledge? ExtractedKnowledge = null, // Knowledge extracted by LLM
    bool? FlaggedForReview = null // True if no knowledge could be extracted
);
