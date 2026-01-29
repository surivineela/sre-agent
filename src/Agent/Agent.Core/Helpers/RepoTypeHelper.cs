// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Helpers;

/// <summary>
/// Type of repository for TSG connectors
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RepoType
{
    AzureDevOps,
    GitHub
}

/// <summary>
/// Helper class for detecting and working with repository types.
/// </summary>
public static class RepoTypeHelper
{
    /// <summary>
    /// Detects the repository type based on the URL.
    /// </summary>
    /// <param name="url">The repository URL.</param>
    /// <returns>The detected repository type (defaults to AzureDevOps if unknown).</returns>
    public static RepoType DetectRepoType(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return RepoType.AzureDevOps;
        }

        try
        {
            var uri = new Uri(url);
            if (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            {
                return RepoType.GitHub;
            }
        }
        catch
        {
            // Invalid URL, default to Azure DevOps
        }

        return RepoType.AzureDevOps;
    }

    /// <summary>
    /// Checks if the URL is a GitHub repository URL.
    /// </summary>
    /// <param name="url">The repository URL.</param>
    /// <returns>True if the URL is a GitHub repository.</returns>
    public static bool IsGitHubUrl(string url) => DetectRepoType(url) == RepoType.GitHub;

    /// <summary>
    /// Checks if the URL is an Azure DevOps repository URL.
    /// </summary>
    /// <param name="url">The repository URL.</param>
    /// <returns>True if the URL is an Azure DevOps repository.</returns>
    public static bool IsAzureDevOpsUrl(string url) => DetectRepoType(url) == RepoType.AzureDevOps;
}
