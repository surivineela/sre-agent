// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models;

/// <summary>
/// Represents a code search result from Azure DevOps
/// </summary>
public class AdoCodeSearchResult
{
    /// <summary>
    /// Path to the file containing the search result
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Commit hash/ID where the file was found
    /// </summary>
    public string Commit { get; set; } = string.Empty;

    /// <summary>
    /// Code snippet containing the search term
    /// </summary>
    public string CodeSnippet { get; set; } = string.Empty;
}

public class GHCodeSearchResult
{
    /// <summary>
    /// Path to the file containing the search result
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Branch, tag, or commit SHA where the file was found
    /// </summary>
    public string Reference { get; set; } = string.Empty;
}
