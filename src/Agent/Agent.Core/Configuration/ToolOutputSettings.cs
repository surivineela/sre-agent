// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration;

/// <summary>
/// Configuration settings for tool output storage
/// </summary>
public class ToolOutputSettings
{
    /// <summary>
    /// Base directory path for storing tool outputs
    /// Default: System temp directory + "SREAgent/ToolOutputs"
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// Number of days to retain tool output files before cleanup
    /// Default: 1 day
    /// </summary>
    public int RetentionDays { get; set; } = 1;

    /// <summary>
    /// Maximum number of characters allowed in tool output retrieval results
    /// Default: 65536 characters (64KB)
    /// </summary>
    public int MaxOutputChars { get; set; } = 65536;

    /// <summary>
    /// Enables the ToolOutputRetriever tool for accessing truncated tool outputs
    /// When enabled, agents will have access to the ToolOutputRetriever tool and related common prompt
    /// Default: false
    /// </summary>
    public bool EnablePartialOutput { get; set; } = false;
}
