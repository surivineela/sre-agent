// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Search.Shared.WebApi.Contracts;
using Microsoft.VisualStudio.Services.Search.WebApi;
using Microsoft.VisualStudio.Services.Search.WebApi.Contracts.Code;
using Microsoft.VisualStudio.Services.WebApi;

namespace Agent.Core.Services;

/// <summary>
/// Service for interacting with Azure DevOps repositories and search capabilities using the official SDK
/// </summary>
public class AzureDevOpsService : IAzureDevOpsService
{
    private readonly ILogger<AzureDevOpsService> _logger;
    private readonly IAuthenticationService _authenticationService;

    public AzureDevOpsService(ILogger<AzureDevOpsService> logger,
                IAuthenticationService authenticationService)
    {
        _logger = logger;
        _authenticationService = authenticationService;
    }

    /// <summary>
    /// Creates a VssConnection for Azure DevOps operations
    /// </summary>
    /// <param name="organization">Azure DevOps organization name</param>
    /// <returns>VssConnection instance</returns>
    private async Task<VssConnection> CreateConnection(string organization)
    {
        var baseUrl = new Uri($"https://dev.azure.com/{organization}");

        var cred = _authenticationService.GetAzureDevOpsCredential();
        var token = await cred.GetTokenAsync(new Azure.Core.TokenRequestContext(new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" }), default);
        var vssCred = new VssBasicCredential(string.Empty, token.Token);

        return new VssConnection(baseUrl, vssCred);
    }

    /// <summary>
    /// Searches for code in Azure DevOps repositories
    /// </summary>
    public async Task<List<AdoCodeSearchResult>> SearchCodeAsync(string organization, string project, string repository, string searchTerm, int top = 50, CancellationToken cancellationToken = default)
    {
        var searchResults = new List<AdoCodeSearchResult>();

        try
        {
            _logger.LogInternalDebug($"Searching for '{searchTerm}' in {organization}/{project}/{repository}");

            using var connection = await CreateConnection(organization);
            var searchClient = connection.GetClient<SearchHttpClient>();

            var searchRequest = new CodeSearchRequest
            {
                SearchText = searchTerm,
                Skip = 0,
                Top = top,
                Filters = new Dictionary<string, IEnumerable<string>>
                {
                    { "Project", new List<string> { project } },
                    { "Repository", new List<string> { repository } },
                    { "Path", new List<string> { "/" } }
                },
                OrderBy = new List<SortOption>
                {
                    new SortOption
                    {
                        Field = "filename",
                        SortOrder = SortOrder.Ascending,
                    }
                },
                IncludeSnippet = true,
            };

            var codeSearchResults = await searchClient.FetchCodeSearchResultsAsync(searchRequest, project, cancellationToken);

            if (codeSearchResults?.Results != null)
            {
                foreach (var result in codeSearchResults.Results)
                {
                    if (!string.IsNullOrEmpty(result.Path))
                    {
                        // Extract branch name from versions if available
                        var branchName = result.Versions?.FirstOrDefault()?.BranchName ?? "main";

                        // Extract commit ID from versions if available
                        var commit = result.Versions?.FirstOrDefault()?.ChangeId ?? string.Empty;

                        // Extract code snippet from matches if available
                        if (result.Matches != null && result.Matches.ContainsKey("content"))
                        {
                            // Get the first match that has content
                            foreach (var hit in result.Matches["content"])
                            {
                                var searchResult = new AdoCodeSearchResult
                                {
                                    Path = result.Path,
                                    Commit = string.IsNullOrEmpty(commit) ? branchName : commit,
                                    CodeSnippet = hit.CodeSnippet,
                                };

                                searchResults.Add(searchResult);
                            }
                        }


                    }
                }

                _logger.LogInternalDebug($"Found {searchResults.Count} code search results for '{searchTerm}'");
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Error searching for code in {organization}/{project}/{repository} with term '{searchTerm}'");
        }

        return searchResults;
    }

    /// <summary>
    /// Gets the content of a specific file from Azure DevOps repository
    /// </summary>
    public async Task<string> GetFileContentAsync(string organization, string project, string repository, string filePath, string commit, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInternalDebug($"Getting file content for {filePath} from {organization}/{project}/{repository}. Commit: {commit}");

            using var connection = await CreateConnection(organization);
            var gitClient = connection.GetClient<GitHttpClient>();

            // Get the repository
            var repo = await gitClient.GetRepositoryAsync(project: project, repositoryId: repository, cancellationToken: cancellationToken);
            if (repo == null)
            {
                _logger.LogInternalWarning($"Repository {repository} not found in project {project}");
                return string.Empty;
            }

            // Get the file item
            var item = await gitClient.GetItemAsync(
                repositoryId: repo.Id,
                path: filePath,
                versionDescriptor: new GitVersionDescriptor
                {
                    VersionType = GitVersionType.Commit,
                    Version = commit,
                },
                includeContent: true,
                cancellationToken: cancellationToken);

            if (item?.Content != null)
            {
                _logger.LogInternalDebug($"Successfully retrieved content for {filePath} ({item.Content.Length} characters)");
                return item.Content;
            }
            else
            {
                _logger.LogInternalWarning($"File {filePath} not found or has no content");
                return string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Error getting file content for {filePath} from {organization}/{project}/{repository}. Commit: {commit}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Checks whether the authenticated client has access to the specified ADO repository.
    /// </summary>
    public async Task<bool> HasRepositoryAccessAsync(string organization, string project, string repository, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await CreateConnection(organization);
            var gitClient = connection.GetClient<GitHttpClient>();

            var repo = await gitClient.GetRepositoryAsync(project: project, repositoryId: repository, cancellationToken: cancellationToken);
            return repo != null;
        }
        catch (VssUnauthorizedException)
        {
            return false;
        }
        catch (VssServiceException ex) when (ex.Message.Contains("TF400813"))
        {
            // Repository may not exist or is inaccessible
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Error checking access to ADO repo {organization}/{project}/{repository}");
            return false;
        }
    }
}
