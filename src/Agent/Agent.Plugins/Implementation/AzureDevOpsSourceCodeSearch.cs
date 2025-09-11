using System.Net.Http.Headers;
using System.Text;
using Agent.Core.Interfaces;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Search.WebApi;
using Microsoft.VisualStudio.Services.Search.WebApi.Contracts.Code;
using Microsoft.VisualStudio.Services.WebApi;

namespace Agent.Plugins.Implementation;

public class AzureDevOpsSourceCodeSearch : IAzureDevOpsSourceCodeSearch
{
    private readonly ILogger<AzureDevOpsSourceCodeSearch> _logger;
    private readonly IAuthenticationService _authenticationService;
    private static readonly HttpClient _httpClient = new HttpClient();
    private static readonly string[] MainBranches = { "main", "master", "develop", "dev" };

    public AzureDevOpsSourceCodeSearch(
        ILogger<AzureDevOpsSourceCodeSearch> logger,
        IAuthenticationService authenticationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
    }

    /// <summary>
    /// Creates a VssConnection for Azure DevOps operations
    /// </summary>
    /// <param name="organization">Azure DevOps organization name</param>
    /// <returns>VssConnection instance</returns>
    private async Task<VssConnection> CreateConnection(string organization)
    {
        var baseUrl = new Uri(organization);
        var cred = _authenticationService.GetAzureDevOpsCredential();
        var token = await cred.GetTokenAsync(new Azure.Core.TokenRequestContext(new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" }), default);
        var vssCred = new VssBasicCredential(string.Empty, token.Token);

        return new VssConnection(baseUrl, vssCred);
    }

    #region Main Search Method

    public async Task<SearchResponse> SearchAsync(string searchTerm, string repoUrl, SearchOptions? options = null)
    {
        var opts = options ?? new SearchOptions();
        (string organization, string project, string repository) = AzureDevOpsWorkItemPlugin.ParseRepositoryUrl(repoUrl);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (opts.Verbose)
            {
                LogSearchStart(searchTerm, opts);
            }

            // Optimize search query for better results
            var optimizedSearchTerm = OptimizeSearchQuery(searchTerm, opts);
            using var connection = await CreateConnection(organization);
            var searchClient = connection.GetClient<SearchHttpClient>();

            var searchRequest = BuildSearchRequest(optimizedSearchTerm, opts);
            var response = await ExecuteSearchWithRetryAsync(searchClient, searchRequest, opts);

            var results = ProcessSearchResults(response, opts);
            var metadata = CreateMetadata(response, results, stopwatch.Elapsed, opts);

            if (opts.Verbose)
            {
                LogSearchComplete(metadata);
            }

            return new SearchResponse
            {
                Results = results,
                Metadata = metadata
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error during Azure DevOps code search for term '{SearchTerm}'", searchTerm);
            
            var errorMetadata = new SearchMetadata
            {
                InfoCode = -1,
                ErrorMessage = ex.Message,
                SearchDuration = stopwatch.Elapsed
            };

            return new SearchResponse
            {
                Results = Array.Empty<ICodeSearchResult>(),
                Metadata = errorMetadata
            };
        }
    }

    #endregion

    #region Helper Methods

    public async Task<string> GetFileContentAsync(string organization, string project, string repositoryId, string filePath)
    {
        var url = $"{organization}/{project}/_apis/git/repositories/{repositoryId}/items" +
                  $"?path={Uri.EscapeDataString(filePath)}&includeContent=true&api-version=7.1-preview.1";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var cred = _authenticationService.GetAzureDevOpsCredential();
        var token = await cred.GetTokenAsync(new Azure.Core.TokenRequestContext(new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" }), default);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token); 
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Optimizes the search query for better Azure DevOps search results
    /// </summary>
    private string OptimizeSearchQuery(string searchTerm, SearchOptions options)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return "*";
        }

        var optimizedTerm = searchTerm.Trim();

        // Add file extension filters directly to search query if specified
        if (options.FileExtensions?.Any() == true)
        {
            var extFilters = options.FileExtensions.Select(ext => $"ext:{ext.TrimStart('.')}");
            optimizedTerm = $"({optimizedTerm}) AND ({string.Join(" OR ", extFilters)})";
        }

        // Add quotes for exact phrase matching if the term contains spaces and isn't already quoted
        if (optimizedTerm.Contains(' ') && !optimizedTerm.StartsWith('"') && !optimizedTerm.EndsWith('"'))
        {
            optimizedTerm = $"\"{optimizedTerm}\"";
        }

        return optimizedTerm;
    }

    private CodeSearchRequest BuildSearchRequest(string searchTerm, SearchOptions options)
    {
        var request = new CodeSearchRequest
        {
            SearchText = searchTerm,
            Top = options.MaxResults,
            Skip = 0,
            IncludeFacets = false,
            IncludeSnippet = options.IncludeSnippets
        };

        var filters = new Dictionary<string, IEnumerable<string>>();

        // Add project filter
        if (!string.IsNullOrEmpty(options.ProjectFilter))
        {
            filters["Project"] = new[] { options.ProjectFilter };
        }

        // Add repository filters
        if (options.Repositories?.Any() == true)
        {
            filters["Repository"] = options.Repositories;
        }

        if (filters.Any())
        {
            request.Filters = filters;
        }

        // Add sorting for better results
        request.OrderBy = new List<Microsoft.VisualStudio.Services.Search.Shared.WebApi.Contracts.SortOption>
        {
            new Microsoft.VisualStudio.Services.Search.Shared.WebApi.Contracts.SortOption
            {
                Field = "filename",
                SortOrder = Microsoft.VisualStudio.Services.Search.Shared.WebApi.Contracts.SortOrder.Ascending,
            }
        };

        return request;
    }

    private async Task<CodeSearchResponse> ExecuteSearchWithRetryAsync(SearchHttpClient searchClient, CodeSearchRequest request, SearchOptions options)
    {
        try
        {
            _logger.LogDebug("Executing Azure DevOps code search with query: {SearchText}", request.SearchText);
            
            var response = await searchClient.FetchCodeSearchResultsAsync(request);

            // Handle error cases with retry logic
            if (response?.InfoCode == 17 || response?.InfoCode == 15)
            {
                if (options.Verbose)
                {
                    _logger.LogInternalInformation("⚠️ Retrying search without project filter...");
                }

                _logger.LogInternalError("Azure DevOps search failed with InfoCode {InfoCode}, retrying without project filter", response.InfoCode);

                // Remove project filter and retry
                var retryRequest = new CodeSearchRequest
                {
                    SearchText = request.SearchText,
                    Top = request.Top,
                    Skip = request.Skip,
                    IncludeFacets = request.IncludeFacets,
                    IncludeSnippet = request.IncludeSnippet,
                    OrderBy = request.OrderBy
                };

                response = await searchClient.FetchCodeSearchResultsAsync(retryRequest);
            }

            _logger.LogInternalInformation("Azure DevOps search completed with InfoCode {InfoCode}", response?.InfoCode ?? -1);
            return response!;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to execute Azure DevOps code search");
            throw;
        }
    }

    private List<ICodeSearchResult> ProcessSearchResults(CodeSearchResponse response, SearchOptions options)
    {
        var results = new List<ICodeSearchResult>();

        if (response?.Results == null) return results;

        foreach (var result in response.Results)
        {
            var branchName = result.Versions?.FirstOrDefault()?.BranchName ?? "main";

            // Apply branch filtering
            if (!options.SearchAllBranches && !IsMainBranch(branchName))
            {
                continue;
            }

            if (options.LimitToMainBranches && !IsMainBranch(branchName))
            {
                continue;
            }

            var searchResult = new CodeSearchResultImpl
            {
                FileName = result.Filename ?? string.Empty,
                FilePath = result.Path ?? string.Empty,
                Repository = result.Repository?.Name ?? "Unknown",
                Project = result.Project?.Name ?? "Unknown",
                Branch = branchName
            };

            // Process matches if requested
            if (options.IncludeSnippets && result.Matches != null)
            {
                foreach (var matchType in result.Matches)
                {
                    if (matchType.Value != null)
                    {
                        foreach (var hit in matchType.Value)
                        {
                            var match = new CodeMatchImpl
                            {
                                LineNumber = hit.Line,
                                ColumnNumber = hit.CharOffset,
                                CodeSnippet = hit.CodeSnippet?.Trim() ?? string.Empty,
                                MatchType = matchType.Key
                            };
                            searchResult.AddMatch(match);
                        }
                    }
                }
            }

            results.Add(searchResult);
        }

        return results;
    }

    private SearchMetadata CreateMetadata(CodeSearchResponse response, List<ICodeSearchResult> results, TimeSpan duration, SearchOptions options)
    {
        var repositories = results.Select(r => r.Repository).Distinct().ToArray();
        var branches = results.Select(r => r.Branch).Distinct().ToArray();
        var fileTypes = results
            .Select(r => Path.GetExtension(r.FileName))
            .Where(ext => !string.IsNullOrEmpty(ext))
            .Distinct()
            .ToArray();

        return new SearchMetadata
        {
            TotalResults = results.Count,
            InfoCode = response?.InfoCode ?? -1,
            Repositories = repositories,
            Branches = branches,
            FileTypes = fileTypes,
            SearchDuration = duration,
            ErrorMessage = GetErrorMessage(response?.InfoCode ?? -1)
        };
    }

    private static bool IsMainBranch(string branchName)
    {
        return MainBranches.Contains(branchName.ToLowerInvariant());
    }

    private static string? GetErrorMessage(int infoCode)
    {
        return infoCode switch
        {
            0 => null,
            6 => "Code Search extension is not installed or enabled",
            15 => "Search syntax error or invalid project filter",
            17 => "Search feature not available for this organization",
            _ => $"Unknown error code: {infoCode}"
        };
    }

    public static string ExtractRelevantLines(string fileContent, int lineNumber, int contextLines = 2)
    {
        if (string.IsNullOrEmpty(fileContent) || lineNumber < 1)
            return "No content available";

        var lines = fileContent.Split('\n');

        // Clamp start/end to valid indices
        int startLine = Math.Max(0, lineNumber - contextLines - 1); // lineNumber is 1-based
        int endLine = Math.Min(lines.Length - 1, lineNumber + contextLines - 1);

        var snippet = new StringBuilder();

        for (int i = startLine; i <= endLine; i++)
        {
            string prefix = (i == lineNumber - 1) ? ">> " : "   "; // Highlight the matched line
            snippet.AppendLine($"{prefix}{i + 1}: {lines[i].TrimEnd()}");
        }

        return snippet.ToString();
    }

    #endregion

    #region Logging Methods

    private void LogSearchStart(string searchTerm, SearchOptions options)
    {
        if (options.Verbose)
        {
            _logger.LogInternalInformation("🔍 Searching for: '{SearchTerm}'", searchTerm);
            _logger.LogInternalInformation("📂 Project filter: {ProjectFilter}", options.ProjectFilter ?? "None (search all projects)");
            _logger.LogInternalInformation("🌿 Branch scope: {BranchScope}", options.SearchAllBranches ? "All branches" : "Default branch only");
            if (options.LimitToMainBranches)
            {
                _logger.LogInternalInformation("🌿 Limited to main branches only");
            }
            _logger.LogInternalInformation("📊 Max results: {MaxResults}", options.MaxResults);
            if (options.FileExtensions?.Any() == true)
            {
                _logger.LogInternalInformation("📄 File types: {FileTypes}", string.Join(", ", options.FileExtensions));
            }
        }
        
        _logger.LogInternalInformation("Starting Azure DevOps code search for '{SearchTerm}' with {MaxResults} max results", 
            searchTerm, options.MaxResults);
    }

    private void LogSearchComplete(SearchMetadata metadata)
    {
        if (metadata.IsSuccess)
        {
            _logger.LogInternalInformation("Azure DevOps search completed successfully: {TotalResults} results in {Duration}ms", 
                metadata.TotalResults, metadata.SearchDuration.TotalMilliseconds);
        }
        else
        {
            _logger.LogInternalInformation("Azure DevOps search completed with issues: InfoCode {InfoCode}, Error: {ErrorMessage}", 
                metadata.InfoCode, metadata.ErrorMessage);
        }

        _logger.LogInternalInformation("📈 Search completed - InfoCode: {InfoCode}", metadata.InfoCode);
        _logger.LogInternalInformation("🎯 Found {TotalResults} results", metadata.TotalResults);
        _logger.LogInternalInformation("⏱️ Duration: {Duration}ms", metadata.SearchDuration.TotalMilliseconds);

        if (metadata.Repositories?.Any() == true)
        {
            _logger.LogInternalInformation("📁 Repositories: {Repositories}", string.Join(", ", metadata.Repositories));
        }

        if (metadata.Branches?.Any() == true)
        {
            _logger.LogInternalInformation("🌿 Branches: {Branches}", string.Join(", ", metadata.Branches));
        }

        if (metadata.FileTypes?.Any() == true)
        {
            _logger.LogInternalInformation("📄 File types: {FileTypes}", string.Join(", ", metadata.FileTypes));
        }

        if (!metadata.IsSuccess && !string.IsNullOrEmpty(metadata.ErrorMessage))
        {
            _logger.LogInternalInformation("⚠️ {ErrorMessage}", metadata.ErrorMessage);
        }
    }

    #endregion
}
