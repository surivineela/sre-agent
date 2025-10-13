// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Graph.Helpers;
using Agent.Graph.Interfaces;
using Microsoft.Extensions.Logging;
using Agent.Core;
using ArmConstants = Agent.Graph.Crawler.ARM.Constants;

namespace Agent.Graph.Crawler.External;

/// <summary>
/// Crawler for source code repositories to detect Azure App Configuration usage
/// and resolve database connections defined in the repository
/// </summary>
public class SourceCodeRepoCrawler : IResourceCrawler
{
    private readonly ILogger _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly AppConfigurationHelper _appConfigurationHelper;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly IGitHubService _gitHubService;

    // Repository type detection patterns
    public const string GithubRepoRegexPattern = @"^https:\/\/(?:github\.com|github\.[\w.-]+\.[\w.-]+)\/[\w.-]+\/[\w.-]+(?:\.git)?$";
    public const string AzDoRepoRegexPattern = @"^https:\/\/(?:(?<org1>dev\.azure\.com)\/(?<organization>[\w-]+)\/(?<project>[\w-]+)|(?<organization>[\w-]+)\.visualstudio\.com\/(?<project>[\w-]+))\/_git\/(?<repo>[\w.-]+)$";

    private readonly string[] _appConfigCodePatterns = new[]
    {
        @"AddAzureAppConfiguration",
    };

    private readonly string[] _appConfigUrlCodePatterns = new[]
    {
        @".azconfig.io",
    };

    private readonly string[] _appConfigUrlPatterns = new[]
    {
        @"https://[\w-]+\.azconfig\.io",
    };

    public SourceCodeRepoCrawler(
        ILogger logger,
        IGraphDatabaseClient graphDbClient,
        AppConfigurationHelper appConfigurationHelper,
        IHttpClientFactory httpClientFactory,
        IAzureDevOpsService azureDevOpsService,
        IGitHubService gitHubService)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _appConfigurationHelper = appConfigurationHelper;
        _httpClientFactory = httpClientFactory;
        _azureDevOpsService = azureDevOpsService;
        _gitHubService = gitHubService;
    }

    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        if (node is not SourceCodeRepoNode sourceCodeNode)
        {
            _logger.LogInternalWarning($"Expected SourceCodeRepoNode but received {node.GetType().Name}");
            yield break;
        }

        _logger.LogInternalInformation($"Crawling source code repository {sourceCodeNode.RepoUrl}");

        // Detect repository type
        var repoType = DetectRepositoryType(sourceCodeNode.RepoUrl);
        _logger.LogInternalInformation($"Detected repository type: {repoType} for {sourceCodeNode.RepoUrl}");

        bool hasAccess = await VerifyRepositoryAccess(sourceCodeNode.RepoUrl, repoType);
        if (!hasAccess)
        {
            yield break;
        }

        List<string> appConfigUrls;
        try
        {
            // Scan repository for App Configuration usage
            appConfigUrls = await ScanForAppConfigurationUsage(sourceCodeNode.RepoUrl, repoType);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error scanning repository for App Configuration usage: {sourceCodeNode.RepoUrl}");
            yield break;
        }

        if (!appConfigUrls.Any())
        {
            _logger.LogInternalInformation($"No Azure App Configuration usage detected in {sourceCodeNode.RepoUrl}");
            yield break;
        }

        _logger.LogInternalInformation($"Found {appConfigUrls.Count} App Configuration references in {sourceCodeNode.RepoUrl}");

        // Process each App Configuration URL to find database connections
        foreach (var appConfigUrl in appConfigUrls)
        {
            _logger.LogInternalInformation($"Processing App Configuration: {appConfigUrl}");

            IAsyncEnumerable<GraphNode> connectedNodes;
            try
            {
                var nodes = await GetRepoServedNodes(sourceCodeNode);
                connectedNodes = _appConfigurationHelper.ProcessAppConfigurationConnections(nodes, appConfigUrl);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error processing App Configuration {appConfigUrl}");
                continue;
            }

            await foreach (var connectedNode in connectedNodes)
            {
                _logger.LogInternalInformation($"Found connected resource from App Configuration: {connectedNode.GetNodeId()}");
                yield return connectedNode;
            }
        }
    }

    /// <summary>
    /// Detects the type of repository (GitHub or Azure DevOps)
    /// </summary>
    private RepositoryType DetectRepositoryType(string repoUrl)
    {
        if (Regex.IsMatch(repoUrl, GithubRepoRegexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return RepositoryType.GitHub;
        }

        if (Regex.IsMatch(repoUrl, AzDoRepoRegexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return RepositoryType.AzureDevOps;
        }

        return RepositoryType.Unknown;
    }

    /// <summary>
    /// Scans the repository for Azure App Configuration usage patterns
    /// </summary>
    private async Task<List<string>> ScanForAppConfigurationUsage(string repoUrl, RepositoryType repoType)
    {
        var appConfigUrls = new List<string>();

        try
        {
            if (repoType == RepositoryType.GitHub)
            {
                appConfigUrls.AddRange(await ScanGitHubRepository(repoUrl));
            }
            else if (repoType == RepositoryType.AzureDevOps)
            {
                appConfigUrls.AddRange(await ScanAzureDevOpsRepository(repoUrl));
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error scanning repository {repoUrl} for App Configuration usage");
        }

        return appConfigUrls;
    }

    /// <summary>
    /// Scans GitHub repository for App Configuration usage
    /// </summary>
    private async Task<List<string>> ScanGitHubRepository(string repoUrl)
    {
        var appConfigUrls = new List<string>();

        try
        {
            var (owner, repo) = ParseGitHubUrl(repoUrl);

            _logger.LogInternalInformation($"Scanning GitHub repository: {owner}/{repo} for App Configuration usage");

            // Phase 1: Check if App Configuration is used by searching for code patterns
            bool appConfigUsageDetected = false;
            foreach (var codePattern in _appConfigCodePatterns)
            {
                try
                {
                    var usageFound = await SearchGitHubCodeForUsagePattern(owner, repo, codePattern);
                    if (usageFound)
                    {
                        appConfigUsageDetected = true;
                        _logger.LogInternalInformation($"Detected App Configuration usage with pattern: {codePattern}");
                        break; // Found usage, no need to check other patterns
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning($"Error searching for code pattern {codePattern}: {ex.Message}");
                }
            }

            if (!appConfigUsageDetected)
            {
                _logger.LogInternalInformation($"No App Configuration usage detected in GitHub repository: {owner}/{repo}");
                return appConfigUrls;
            }

            // Phase 2: Since App Configuration is used, search for endpoint patterns to get URLs
            _logger.LogInternalInformation($"App Configuration usage confirmed, searching for endpoint URLs in {owner}/{repo}");

            foreach (var searchTerm in _appConfigUrlCodePatterns)
            {
                try
                {
                    var searchResults = await SearchGitHubCodeForPattern(owner, repo, searchTerm);
                    appConfigUrls.AddRange(searchResults);

                    if (searchResults.Any())
                    {
                        _logger.LogInternalInformation($"Found {searchResults.Count} App Configuration URLs for search term {searchTerm}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning($"Error searching for search term {searchTerm}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error scanning GitHub repository {repoUrl}");
        }

        return appConfigUrls.Distinct().ToList();
    }

    /// <summary>
    /// Scans Azure DevOps repository for App Configuration usage
    /// </summary>
    private async Task<List<string>> ScanAzureDevOpsRepository(string repoUrl)
    {
        var appConfigUrls = new List<string>();

        try
        {
            var (org, project, repo) = ParseAzureDevOpsUrl(repoUrl);

            _logger.LogInternalInformation($"Scanning Azure DevOps repository: {org}/{project}/{repo}");

            // Phase 1: Check if App Configuration is used by searching for code patterns
            bool appConfigUsageDetected = false;
            foreach (var codePattern in _appConfigCodePatterns)
            {
                try
                {
                    var searchResults = await SearchAzureDevOpsCodeForUsagePattern(org, project, repo, codePattern);
                    if (searchResults)
                    {
                        appConfigUsageDetected = true;
                        _logger.LogInternalInformation($"Detected App Configuration usage with pattern: {codePattern}");
                        break; // Found usage, no need to check other patterns
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning($"Error searching Azure DevOps for code pattern {codePattern}: {ex.Message}");
                }
            }

            if (!appConfigUsageDetected)
            {
                _logger.LogInternalInformation($"No App Configuration usage detected in Azure DevOps repository: {org}/{project}/{repo}");
                return appConfigUrls;
            }

            // Phase 2: Since App Configuration is used, search for endpoint patterns to get URLs
            _logger.LogInternalInformation($"App Configuration usage confirmed, searching for endpoint URLs in {org}/{project}/{repo}");

            foreach (var searchTerm in _appConfigUrlCodePatterns)
            {
                try
                {
                    var searchResults = await SearchAzureDevOpsCodeForPattern(org, project, repo, searchTerm);
                    appConfigUrls.AddRange(searchResults);

                    if (searchResults.Any())
                    {
                        _logger.LogInternalInformation($"Found {searchResults.Count} App Configuration URLs for search term {searchTerm}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning($"Error searching Azure DevOps for search term {searchTerm}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error scanning Azure DevOps repository {repoUrl}");
        }

        return appConfigUrls.Distinct().ToList();
    }

    /// <summary>
    /// Parses GitHub URL to extract owner and repository name
    /// </summary>
    private static (string owner, string repo) ParseGitHubUrl(string repoUrl)
    {
        if (string.IsNullOrWhiteSpace(repoUrl))
        {
            throw new ArgumentException("Repository URL cannot be empty.");
        }

        string regexPattern = @"github\.com[/:](?<owner>[\w.-]+)/(?<repo>[\w.-]+?)(?=\.git(?:[/?#]|$)|[/?#]|$)";
        string errorMessage = $"Repository URL must be of the form https://github.com/owner/repo-name.git whereas the supplied repoUrl is {repoUrl}";
        if (repoUrl.Contains("/repos/", StringComparison.OrdinalIgnoreCase))
        {
            // Ensure repo capture stops before next segment/query/fragment
            regexPattern = @"github\.com/repos[/:](?<owner>[\w.-]+)/(?<repo>[\w.-]+?)(?=[/?#]|$)";
            errorMessage = $"Repository URL must be of the form https://github.com/repos/owner/repo-name whereas the supplied repoUrl is {repoUrl}";
        }

        var match = Regex.Match(repoUrl, regexPattern);
        if (!match.Success)
        {
            throw new ArgumentException(errorMessage);
        }

        return (match.Groups["owner"].Value, match.Groups["repo"].Value);
    }

    /// <summary>
    /// Parses Azure DevOps URL to extract organization, project, and repository name
    /// </summary>
    private static (string org, string project, string repo) ParseAzureDevOpsUrl(string repoUrl)
    {
        try
        {
            var uri = new Uri(repoUrl);
            string org, project, repo;

            if (uri.Host == "dev.azure.com" || uri.Host.Contains(".dev.azure.com"))
            {
                // Format: https://dev.azure.com/{org}/{project}/_git/{repo}
                var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 4 || parts[parts.Length - 2] != "_git")
                {
                    throw new ArgumentException("Invalid repository URL format");
                }

                org = parts[0];
                project = parts[1];
                repo = parts[parts.Length - 1];
            }
            else if (uri.Host.EndsWith("visualstudio.com"))
            {
                // Format: https://{org}.visualstudio.com/{project}/_git/{repo}
                var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 3 || parts[1] != "_git")
                {
                    throw new ArgumentException("Invalid repository URL format");
                }

                org = uri.Host.Split('.')[0];
                project = parts[0];
                repo = parts[2];
            }
            else
            {
                throw new ArgumentException("Unsupported repository URL format");
            }

            return (org, project, repo);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to parse repository URL: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extracts App Configuration URLs from file content using comprehensive wildcard patterns
    /// </summary>
    private List<string> ExtractAppConfigUrlsFromContent(string content)
    {
        var urls = new List<string>();

        if (string.IsNullOrEmpty(content))
            return urls;

        foreach (var pattern in _appConfigUrlPatterns)
        {
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);

            foreach (Match match in matches)
            {
                urls.Add(match.Value);
            }
        }

        return urls.Distinct().ToList();
    }

    /// <summary>
    /// Searches GitHub repository code for App Configuration usage patterns (returns boolean)
    /// </summary>
    private async Task<bool> SearchGitHubCodeForUsagePattern(string owner, string repo, string searchPattern)
    {
        try
        {
            var searchResults = await _gitHubService.SearchCodeAsync(owner, repo, searchPattern);
            return searchResults.Any();
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Error searching GitHub for code pattern {searchPattern}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Searches GitHub repository code for a specific search term and returns file content
    /// </summary>
    private async Task<List<string>> SearchGitHubCodeForPattern(string owner, string repo, string searchTerm)
    {
        var urls = new List<string>();
        try
        {
            var searchResults = await _gitHubService.SearchCodeAsync(owner, repo, searchTerm);

            HashSet<string> uniqueFiles = new HashSet<string>();

            foreach (var searchResult in searchResults)
            {
                try
                {
                    if (uniqueFiles.Contains(searchResult.Path))
                    {
                        continue;
                    }

                    var content = await _gitHubService.GetFileContentAsync(owner, repo, searchResult.Path, searchResult.Reference);
                    uniqueFiles.Add(searchResult.Path);

                    if (!string.IsNullOrEmpty(content))
                    {
                        urls.AddRange(ExtractAppConfigUrlsFromContent(content));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning($"Error processing file {searchResult.Path}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Error searching GitHub for code pattern {searchTerm}: {ex.Message}");
        }

        return urls.Distinct().ToList();
    }

    /// <summary>
    /// Searches Azure DevOps repository code for App Configuration usage patterns (returns boolean)
    /// </summary>
    private async Task<bool> SearchAzureDevOpsCodeForUsagePattern(string org, string project, string repo, string searchPattern)
    {
        try
        {
            var searchResults = await _azureDevOpsService.SearchCodeAsync(org, project, repo, searchPattern, top: 1);
            return searchResults.Any();
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Error searching Azure DevOps for code pattern {searchPattern}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Searches Azure DevOps repository code for a specific search term and returns extracted URLs using regex
    /// </summary>
    private async Task<List<string>> SearchAzureDevOpsCodeForPattern(string org, string project, string repo, string searchTerm)
    {
        var urls = new List<string>();

        try
        {
            // Get search results that contain the search term
            var searchResults = await _azureDevOpsService.SearchCodeAsync(org, project, repo, searchTerm);

            HashSet<string> uniqueFiles = new HashSet<string>();
            // For each file, get the content and extract URLs
            foreach (var searchResult in searchResults)
            {
                try
                {
                    string content = searchResult.CodeSnippet;
                    if (string.IsNullOrEmpty(content))
                    {
                        if (uniqueFiles.Contains(searchResult.Path))
                        {
                            continue;
                        }
                        content = await _azureDevOpsService.GetFileContentAsync(org, project, repo, searchResult.Path, searchResult.Commit);
                        uniqueFiles.Add(searchResult.Path);
                    }

                    if (!string.IsNullOrEmpty(content))
                    {
                        var extractedUrls = ExtractAppConfigUrlsFromContent(content);
                        urls.AddRange(extractedUrls);

                        if (extractedUrls.Any())
                        {
                            _logger.LogInternalInformation($"Found App Configuration URLs in {searchResult.Path}: {string.Join(", ", extractedUrls)}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning($"Error processing file {searchResult.Path}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Error searching Azure DevOps for search term {searchTerm}: {ex.Message}");
        }

        return urls.Distinct().ToList();
    }
    private async Task<List<GraphNode>> GetRepoServedNodes(SourceCodeRepoNode sourceCodeNode)
    {
        var nodes = new List<GraphNode>();

        var query = @$"g.V().has(id, '{sourceCodeNode.GetNodeId()}').inE('{ArmConstants.Relationships.ServesCode}').outV().has('isDeleted', false).valueMap()";
        var result = await _graphDbClient.Query(query);

        if (result != null)
        {
            foreach (var item in result)
            {
                var node = new ArmResourceNode(item);
                nodes.Add(node);
            }
        }

        _logger.LogInternalInformation($"Found {nodes.Count} nodes served by repository {sourceCodeNode.GetNodeId()}");

        return nodes;
    }

    private async Task<bool> VerifyRepositoryAccess(string repoUrl, RepositoryType repoType)
    {
        try
        {
            switch (repoType)
            {
                case RepositoryType.GitHub:
                    var (owner, ghRepo) = ParseGitHubUrl(repoUrl);
                    _logger.LogInternalInformation($"Verifying access to GitHub repository {owner}/{ghRepo}");
                    var ghAccess = await _gitHubService.HasRepositoryAccessAsync(owner, ghRepo);
                    if (!ghAccess)
                    {
                        _logger.LogInternalWarning($"No access to GitHub repository {owner}/{ghRepo}. Ensure the configured GitHub token has read access to this repo.");
                    }
                    return ghAccess;

                case RepositoryType.AzureDevOps:
                    var (org, project, adoRepo) = ParseAzureDevOpsUrl(repoUrl);
                    _logger.LogInternalInformation($"Verifying access to Azure DevOps repository {org}/{project}/{adoRepo}");
                    var adoAccess = await _azureDevOpsService.HasRepositoryAccessAsync(org, project, adoRepo);
                    if (!adoAccess)
                    {
                        _logger.LogInternalWarning($"No access to Azure DevOps repository {org}/{project}/{adoRepo}. Ensure the configured credentials grant read access to this repository.");
                    }
                    return adoAccess;

                default:
                    _logger.LogInternalWarning($"Unknown or unsupported repository type for URL: {repoUrl}");
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to verify repository access for {repoUrl}");
            return false;
        }
    }
}

/// <summary>
/// Enumeration of supported repository types
/// </summary>
public enum RepositoryType
{
    Unknown,
    GitHub,
    AzureDevOps
}
