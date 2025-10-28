// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public record MemorySearchResult(
    string ResourceId,
    string Symptoms,
    List<TrajectoryResult> SameResourceTrajectories,
    List<TrajectoryResult> SimilarSymptomsTrajectories,
    List<string> UserMemories,
    List<DocumentResult> Documents,
    DateTime Timestamp,
    int TotalResults
);

public record TrajectoryResult(
    string Id,
    string Title,
    string InitialSymptoms,
    string SymptomsObserved,
    string StepsFollowed,
    string RootCause,
    string Pitfalls
);

public record DocumentResult(
    string Id,
    string Title,
    string DocumentType,
    string? Summary,
    string? Content,
    string? Url,
    double? RelevanceScore
);