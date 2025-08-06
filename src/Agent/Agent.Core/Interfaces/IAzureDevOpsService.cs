// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;

namespace Agent.Core.Interfaces;

/// <summary>
/// Service for interacting with Azure DevOps repositories and search capabilities
/// </summary>
public interface IAzureDevOpsService
{
    /// <summary>
    /// Searches for code in Azure DevOps repositories
    /// </summary>
    /// <param name="organization">Azure DevOps organization name</param>
    /// <param name="project">Project name</param>
    /// <param name="repository">Repository name</param>
    /// <param name="searchTerm">Term to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of code search results containing path, branch, commit, and code snippet</returns>
    Task<List<AdoCodeSearchResult>> SearchCodeAsync(string organization, string project, string repository, string searchTerm, int top = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the content of a specific file from Azure DevOps repository
    /// </summary>
    /// <param name="organization">Azure DevOps organization name</param>
    /// <param name="project">Project name</param>
    /// <param name="repository">Repository name</param>
    /// <param name="filePath">Path to the file</param>
    /// <param name="ref">Ref name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content as string</returns>
    Task<string> GetFileContentAsync(string organization, string project, string repository, string filePath, string commit, CancellationToken cancellationToken = default);
}
