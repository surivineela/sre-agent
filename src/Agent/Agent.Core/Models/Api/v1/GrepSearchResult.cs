// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

/// <summary>
/// Structured result from a grep search operation for rich UI rendering.
/// </summary>
public class GrepSearchResult
{
    /// <summary>
    /// Total number of matches found across all files.
    /// </summary>
    public int TotalMatches { get; set; }

    /// <summary>
    /// The search query used.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Whether the query was a regex pattern.
    /// </summary>
    public bool IsRegex { get; set; }

    /// <summary>
    /// Grouped results by file.
    /// </summary>
    public List<GrepFileResult> Files { get; set; } = new();

    /// <summary>
    /// Whether results were truncated due to limit.
    /// </summary>
    public bool IsTruncated { get; set; }

    /// <summary>
    /// The maximum results limit that was applied.
    /// </summary>
    public int MaxResults { get; set; }
}

/// <summary>
/// Results for a single file in a grep search.
/// </summary>
public class GrepFileResult
{
    /// <summary>
    /// Relative path to the file from sandbox root.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Number of matches in this file.
    /// </summary>
    public int MatchCount { get; set; }

    /// <summary>
    /// Individual line matches with context.
    /// </summary>
    public List<GrepLineMatch> Matches { get; set; } = new();
}

/// <summary>
/// A single line match or context line in grep results.
/// </summary>
public class GrepLineMatch
{
    /// <summary>
    /// 1-based line number of the match.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// The full line content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a context line (not a match line).
    /// </summary>
    public bool IsContext { get; set; }

    /// <summary>
    /// Start and end positions of matches within the line (for highlighting).
    /// </summary>
    public List<MatchRange> MatchRanges { get; set; } = new();
}

/// <summary>
/// Character range for highlighting a match within a line.
/// </summary>
public class MatchRange
{
    /// <summary>
    /// 0-based start character index.
    /// </summary>
    public int Start { get; set; }

    /// <summary>
    /// 0-based end character index (exclusive).
    /// </summary>
    public int End { get; set; }
}
