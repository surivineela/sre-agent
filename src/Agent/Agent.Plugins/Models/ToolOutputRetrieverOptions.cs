// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Models;

/// <summary>
/// Options for accessing stored tool outputs.
/// </summary>
public class ToolOutputRetrieverOptions
{
    /// <summary>
    /// Unique ID for the stored file.
    /// </summary>
    public required string FileKey { get; set; }

    /// <summary>
    /// Operation to perform: read_by_line, read_by_offset, summarize, filter_structured, search_regex.
    /// </summary>
    public required string Operation { get; set; }

    /// <summary>
    /// Starting line number (1-based, for read_by_line and summarize with scope=lines).
    /// </summary>
    public int? LineStart { get; set; }

    /// <summary>
    /// Ending line number (1-based, optional, for read_by_line and summarize with scope=lines).
    /// </summary>
    public int? LineEnd { get; set; }

    /// <summary>
    /// Starting byte offset (0-based, for read_by_offset and summarize with scope=offset).
    /// </summary>
    public long? OffsetStart { get; set; }

    /// <summary>
    /// Ending byte offset (0-based, optional, for read_by_offset and summarize with scope=offset).
    /// </summary>
    public long? OffsetEnd { get; set; }

    /// <summary>
    /// Prompt for summarization (required for summarize operation).
    /// </summary>
    public string? SummaryPrompt { get; set; }

    /// <summary>
    /// JMESPath expression for filtering (required for filter_structured operation).
    /// </summary>
    public string? JmesPath { get; set; }

    /// <summary>
    /// Regex pattern to search for (required for search_regex operation).
    /// </summary>
    public string? RegexPattern { get; set; }

    /// <summary>
    /// Regex flags: i=case-insensitive, m=multiline, s=dot-matches-newline (optional for search_regex).
    /// </summary>
    public string? RegexFlags { get; set; }

    /// <summary>
    /// Maximum number of regex matches to return (default: 100).
    /// </summary>
    public int? RegexMaxMatches { get; set; }
}
