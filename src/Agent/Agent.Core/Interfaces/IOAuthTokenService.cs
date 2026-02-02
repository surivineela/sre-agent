// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Core.Interfaces;

/// <summary>
/// Service for managing OAuth tokens including retrieval and refresh
/// </summary>
public interface IOAuthTokenService
{
    /// <summary>
    /// Gets a valid GitHub OAuth access token. If the token is expired, it will be refreshed automatically.
    /// </summary>
    /// <returns>A valid GitHub access token, or null if no token exists or refresh fails</returns>
    Task<GitHubAccessToken?> GetValidGitHubTokenAsync();

    /// <summary>
    /// Gets a valid Azure DevOps OAuth access token for the specified organization.
    /// If the token is expired, it will be refreshed automatically.
    /// </summary>
    /// <param name="organizationName">The name of the Azure DevOps organization</param>
    /// <returns>A valid Azure DevOps access token, or null if no token exists or refresh fails</returns>
    Task<AzureDevOpsAccessToken?> GetValidAzureDevOpsTokenAsync(string organizationName);
}
