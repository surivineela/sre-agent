// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace Agent.Core.Services;

/// <summary>
/// Service for interacting with GitHub repositories and search capabilities using the official SDK
/// </summary>
public class GitHubService : IGitHubService
{
    private readonly ILogger<GitHubService> _logger;
    private readonly IAuthenticationService _authenticationService;

    public GitHubService(ILogger<GitHubService> logger,
                IAuthenticationService authenticationService)
    {
        _logger = logger;
        _authenticationService = authenticationService;
    }

    /// <summary>
    /// Creates a GitHubClient for GitHub operations
    /// </summary>
    /// <returns>GitHubClient instance</returns>
    private async Task<GitHubClient> CreateClient()
    {
        var client = new GitHubClient(new ProductHeaderValue("SREAgent"));

        var token = await _authenticationService.GetGitHubAccessToken();
        client.Credentials = new Credentials(token, AuthenticationType.Bearer);

        return client;
    }

    /// <summary>
    /// Searches for code in GitHub repositories
    /// </summary>
    public async Task<List<GHCodeSearchResult>> SearchCodeAsync(string owner, string repository, string searchTerm, int top = 50, CancellationToken cancellationToken = default)
    {
        var searchResults = new List<GHCodeSearchResult>();

        try
        {
            _logger.LogInternalDebug($"Searching for '{searchTerm}' in {owner}/{repository}");

            var client = await CreateClient();

            var searchRequest = new SearchCodeRequest(searchTerm)
            {
                Repos = new RepositoryCollection { { owner, repository } },
                PerPage = Math.Min(top, 100) // GitHub API limit is 100 per page
            };

            var codeSearchResult = await client.Search.SearchCode(searchRequest);

            if (codeSearchResult?.Items != null)
            {
                foreach (var item in codeSearchResult.Items)
                {
                    if (!string.IsNullOrEmpty(item.Path))
                    {
                        var url = new Uri(item.Url);
                        var queryParams = url.ParseQueryString();
                        var reference = queryParams["ref"] ?? item.Repository.DefaultBranch;
                        var searchResult = new GHCodeSearchResult
                        {
                            Path = item.Path,
                            Reference = reference
                        };

                        searchResults.Add(searchResult);
                    }
                }

                _logger.LogInternalDebug($"Found {searchResults.Count} code search results for '{searchTerm}'");
            }
        }
        catch (RateLimitExceededException ex)
        {
            _logger.LogInternalWarning(ex, $"GitHub rate limit exceeded while searching for '{searchTerm}' in {owner}/{repository}");
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Error searching for code in {owner}/{repository} with term '{searchTerm}'");
        }

        return searchResults;
    }

    /// <summary>
    /// Gets the content of a specific file from GitHub repository
    /// </summary>
    public async Task<string> GetFileContentAsync(string owner, string repository, string path, string reference, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInternalDebug($"Getting file content for {owner}/{repository}/{path}:{reference}");

            var client = await CreateClient();

            // Get the file content
            var fileContent = await client.Repository.Content.GetAllContentsByRef(owner, repository, path, reference);

            if (fileContent != null && fileContent.Count > 0)
            {
                var content = fileContent[0].Content;
                _logger.LogInternalDebug($"Successfully retrieved content for {path} ({content?.Length ?? 0} characters)");
                return content ?? string.Empty;
            }
            else
            {
                _logger.LogInternalWarning($"File {path} not found or has no content");
                return string.Empty;
            }
        }
        catch (NotFoundException)
        {
            _logger.LogInternalWarning($"File {path} not found in {owner}/{repository}/{path}:{reference}");
            return string.Empty;
        }
        catch (RateLimitExceededException ex)
        {
            _logger.LogInternalWarning(ex, $"GitHub rate limit exceeded while getting file content for {path} from {owner}/{repository}:{reference}");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Error getting file content for {path} from {owner}/{repository}:{reference}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Checks whether the authenticated client has access to the specified repository.
    /// </summary>
    public async Task<bool> HasRepositoryAccessAsync(string owner, string repository, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateClient();
            var repo = await client.Repository.Get(owner, repository);
            return repo != null;
        }
        catch (NotFoundException)
        {
            // Returned when repo doesn't exist or is private without access
            return false;
        }
        catch (AuthorizationException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Error checking access to {owner}/{repository}");
            return false;
        }
    }
}
