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
    /// Default: 16384 characters (16KB)
    /// </summary>
    public int MaxOutputChars { get; set; } = 16384;
}
