// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;

namespace Agent.Core.Interfaces;

/// <summary>
/// Service for interacting with GitHub repositories and search capabilities
/// </summary>
public interface IGitHubService
{
    /// <summary>
    /// Searches for code in GitHub repositories
    /// </summary>
    /// <param name="owner">GitHub repository owner</param>
    /// <param name="repository">Repository name</param>
    /// <param name="searchTerm">Term to search for</param>
    /// <param name="top">Maximum number of results to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of code search results containing path, branch, commit, and code snippet</returns>
    Task<List<GHCodeSearchResult>> SearchCodeAsync(string owner, string repository, string searchTerm, int top = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the content of a specific file from GitHub repository
    /// </summary>
    /// <param name="owner">GitHub repository owner</param>
    /// <param name="repository">Repository name</param>
    /// <param name="filePath">Path to the file</param>
    /// <param name="reference">Branch, tag, or commit SHA</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content as string</returns>
    Task<string> GetFileContentAsync(string owner, string repository, string path, string reference, CancellationToken cancellationToken = default);
}
