// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Data.DataModels;
using Agent.Framework;

namespace Agent.Runtime.Interfaces;

/// <summary>
/// Service interface for code repository operations.
/// </summary>
public interface ICodeRepoService
{
    /// <summary>
    /// Creates or updates a code repository.
    /// </summary>
    /// <param name="repoName">The repository name.</param>
    /// <param name="model">The repository model.</param>
    /// <returns>The repository document.</returns>
    Task<CodeRepoDocumentModel> CreateOrUpdateCodeRepoAsync(
        string repoName, CodeRepoDocumentModel model);

    /// <summary>
    /// Gets a code repository by name.
    /// </summary>
    /// <param name="repoName">The repository name.</param>
    /// <returns>The repository document, or null if not found.</returns>
    Task<CodeRepoDocumentModel?> GetCodeRepoAsync(string repoName);

    /// <summary>
    /// Gets all code repositories.
    /// </summary>
    /// <returns>Array of all repository documents.</returns>
    Task<CodeRepoDocumentModel[]> GetCodeReposAsync();

    /// <summary>
    /// Deletes a code repository.
    /// </summary>
    /// <param name="repoName">The repository name.</param>
    /// <returns>The deleted repository document, or null if not found.</returns>
    Task<CodeRepoDocumentModel?> DeleteCodeRepoAsync(string repoName);

    /// <summary>
    /// Gets an access token for a code repository.
    /// For Azure DevOps: Uses managed identity if configured, otherwise uses OAuth token service.
    /// For GitHub: Uses OAuth token service.
    /// </summary>
    /// <param name="repoUrl">The repository URL.</param>
    /// <param name="repoType">The repository type.</param>
    /// <returns>The access token, or null if unable to retrieve.</returns>
    Task<string?> GetCodeRepoTokenAsync(string repoUrl, RepoType repoType);

    /// <summary>
    /// Finds the authentication connector name for a code repository.
    /// </summary>
    /// <param name="repoUrl">The repository URL.</param>
    /// <param name="repoType">The repository type.</param>
    /// <returns>The connector name, or null if no suitable connector is found.</returns>
    DataConnectorBasicInfo? FindAuthConnector(string repoUrl, RepoType repoType);
}
