// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

/// <summary>
/// Structured result from a file read operation for rich UI rendering.
/// </summary>
public class ReadFileResult
{
    /// <summary>
    /// Relative path to the file from sandbox root.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 1-based start line number.
    /// </summary>
    public int StartLine { get; set; }

    /// <summary>
    /// 1-based end line number (inclusive).
    /// </summary>
    public int EndLine { get; set; }

    /// <summary>
    /// Total number of lines in the file.
    /// </summary>
    public int TotalLines { get; set; }

    /// <summary>
    /// The file content (lines joined with \n).
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Whether the content was truncated due to line limit.
    /// </summary>
    public bool IsTruncated { get; set; }

    /// <summary>
    /// Error message if the read failed.
    /// </summary>
    public string? Error { get; set; }
}
