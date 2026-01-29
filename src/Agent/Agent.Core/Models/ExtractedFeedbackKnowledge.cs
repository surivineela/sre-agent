// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models;

/// <summary>
/// Types of knowledge that can be extracted from feedback
/// </summary>
public enum FeedbackKnowledgeType
{
    InvestigationPattern,
    SystemDesignKnowledge,
    Other
}

/// <summary>
/// Represents a single piece of extracted knowledge from user feedback
/// </summary>
public record FeedbackKnowledgeItem(
    FeedbackKnowledgeType Type,
    string Comment
);

/// <summary>
/// Container for all extracted knowledge from user feedback
/// </summary>
public record ExtractedFeedbackKnowledge(
    List<FeedbackKnowledgeItem> KnowledgeItems,
    string OriginalComment,
    DateTime SubmittedAt
);
