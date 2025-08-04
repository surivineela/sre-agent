// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

/// <summary>
/// Represents a task progress update that can be streamed to clients
/// </summary>
public sealed record TaskProgressUpdate
{
    public required Guid TaskId { get; set; }
    public required string Phase { get; set; } // "initial_investigation", "forming_hypothesis", "conclusion"
    public required string Status { get; set; } // "started", "in_progress", "completed", "failed"
    public required string Message { get; set; }
    public required DateTime Timestamp { get; set; }
    public string? Summary { get; set; }
    public ConclusionProperties? Conclusion { get; set; }
    public HypothesisTreeItem? HypothesisUpdate { get; set; }
    public string? HypothesisAction { get; set; } // "add", "update", "validate"
} 