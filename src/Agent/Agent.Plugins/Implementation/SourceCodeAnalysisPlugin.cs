using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Plugins.Helpers;
using Agent.Plugins.Interface;
using Agent.Plugins.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Octokit;
using Agent.Framework;

namespace Agent.Plugins.Implementation;

public class SourceCodeAnalysisPlugin : ISourceCodeAnalysisPlugin
{
    private readonly GitHubClient _gitHubClient;
    private readonly IGithubIssuePlugin _githubIssuePlugin;
    private readonly IAzureDevOpsSourceCodeSearch _azureDevOpsSourceCodeSearch;
    private readonly IAzureDevOpsWorkItemPlugin _azureDevOpsWorkItemPlugin;
    private readonly ILogger<SourceCodeAnalysisPlugin> _logger;
    private readonly IChatClientProvider _chatClientProvider;
    private static readonly HttpClient _httpClient = new();

    private const string SYSTEM_PROMPT = @"
SYSTEM ROLE:
You are an expert AI that converts natural language developer queries into precise Azure DevOps Code Search query syntax.

---

TASK:
Transform a semantic query into Azure DevOps Code Search lexical queries.
Generate keyword-based search queries that match actual code patterns.
Limit to maximum 15 search queries to keep results focused and manageable.
IMPORTANT: Rank queries from most to least relevant - put the most specific/pertinent queries FIRST.

---

AZURE DEVOPS CODE SEARCH SYNTAX:

OPERATORS (must be UPPERCASE):
- AND: Combines terms (both must be present)
- OR: Alternative terms (either can be present)  
- NOT: Excludes terms

FILTERS:
- file:filename - Specific filename or pattern (e.g., file:Startup.cs, file:*.config)
- ext:.extension - File extension (e.g., ext:.cs, ext:.js)
- path:/folder/ - Under specific path (e.g., path:/Controllers/, path:/src/api/)
- repo:name - Inside repository (only if explicitly mentioned)
- proj:name - Inside project (only if explicitly mentioned)
- class:name - Class name
- comment:text - Comment text
- def:name - Definition
- decl:name - Declaration
- method:name - Method name
- namespace:name - Namespace
- type:name - Type name
- field:name - Field name
- interface:name - Interface name

SPECIAL FEATURES:
- Double quotes for exact phrases: ""exact phrase""
- Wildcards: * (multiple chars), ? (single char)
- Parentheses for grouping: (term1 OR term2) AND term3

CRITICAL SYNTAX RULES - READ CAREFULLY:
- Use ONLY single double quotes: ""text""
- NEVER use triple quotes: """"text""""
- NEVER put quoted phrases side-by-side without AND/OR/NOT
- ALWAYS use AND to combine multiple required terms
- Use OR for alternatives/variations
- Operators MUST be UPPERCASE: AND OR NOT
- Filters use lowercase with colon (e.g., file:.cs, path:/api/)

CORRECT EXAMPLES:
✓ ""public IActionResult"" AND ""return View()"" file:.cs
✓ (HttpClient OR RestClient) AND SendAsync file:.cs
✓ ""async Task"" AND await file:.cs
✓ class:UserController AND method:Index
✓ def:Authenticate path:/Controllers/ file:.cs

INCORRECT EXAMPLES (DO NOT GENERATE THESE):
✗ """"public IActionResult"""" """"return View()"""" file:.cs
✗ ""phrase1"" ""phrase2"" file:.cs
✗ ""public IActionResult"", ""return View()"" file:.cs

---

OUTPUT FORMAT:

Single concept query: Return raw string only (no JSON, no array)
Example: ""HttpClient"" AND ""SendAsync"" file:.cs

Multiple search strategies: Return JSON array of strings
- IMPORTANT: Limit to maximum 10 queries
- IMPORTANT: Rank by relevance - most specific first
- Format: Valid JSON array with escaped quotes

JSON Format Requirements:
- Valid JSON parseable by System.Text.Json
- Escape quotes with backslash: \""
- NO markdown code blocks
- NO explanations or comments

---

QUERY CONSTRUCTION STRATEGY:

Start BROAD, then get SPECIFIC. Generate queries in this order:

1. **Broad Simple Queries First** (high recall, cast wide net)
   - Use simple keywords without complex filters
   - Rely on basic terms and file extensions
   - Examples: ""ResetPassword"" file:.cs, ""UserController"", authentication

2. **Medium Specificity** (balanced precision/recall)
   - Add path filters or combine with AND
   - Examples: ""Login"" path:/Controllers/, password AND reset

3. **Highly Specific Queries** (high precision, narrow results)
   - Use semantic filters (class:, method:, def:)
   - Combine multiple filters and operators
   - Examples: class:UserController AND method:Login, def:Authenticate path:/api/

TIPS:
- Start with 1-3 broad queries to maximize coverage
- Follow with specific queries for precision
- Use OR for variations (""ResetPassword"" OR ""PasswordReset"")
- Add wildcards (*) for partial matches when needed
- Limit total to 10 queries maximum

---

EXAMPLES (showing broad-to-specific ordering):

Input:
Find MVC Index actions that return views

Output:
[""Index file:.cs"", ""return View()"" path:/Controllers/"", ""IActionResult Index"" file:.cs"", ""method:Index AND """"return View()"""" path:/Controllers/"", class:*Controller AND method:Index""]

Input:
Where do we create the JWT in the auth service?

Output:
[""JWT file:.cs"", (JWT OR JsonWebToken) path:/auth/"", JWT AND (create OR generate) file:.cs"", def:CreateToken OR def:GenerateToken""]

Input:
Find JavaScript or TypeScript files that run SQL queries

Output:
[""SELECT file:.js"", ""SELECT file:.ts"", sql AND query ext:.js"", sql AND query ext:.ts"", ""SELECT"" AND (query OR execute) file:*.js""]

Input:
Look for all UserController class definitions

Output:
[""UserController file:.cs"", ""class UserController"", class:UserController"", def:UserController file:.cs""]

Input:
Where do we handle password reset?

Output:
[""password file:.cs"", ""ResetPassword"" OR ""PasswordReset"", password AND reset path:/Controllers/"", method:ResetPassword OR method:PasswordReset""]

Input:
Find async functions in TypeScript

Output:
[""async file:.ts"", async AND function file:.ts"", ""async function"" ext:.ts""]

Input:
Search for API endpoints handling user data

Output:
[""User file:.cs"", User AND api path:/Controllers/"", class:UserController OR class:UsersController"", ""[HttpGet]"" AND User path:/api/""]

Input:
Find all interfaces in the domain layer

Output:
[""interface path:/Domain/"", ""interface:I*"", ""public interface"" path:/Domain/ file:.cs""]

Input:
Locate database connection strings

Output:
[""ConnectionString"", ""ConnectionString"" file:*.config"", ""ConnectionString"" file:*.json"", def:ConnectionString""]
";


    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "X-GitHub-Token",
        "Cookie",
        "Set-Cookie"
    };

    public SourceCodeAnalysisPlugin(Models.GitHubClient gitHubClient,
                                    ILogger<SourceCodeAnalysisPlugin> logger,
                                    IGithubIssuePlugin githubIssuePlugin,
                                    IAzureDevOpsSourceCodeSearch azureDevOpsSourceCodeSearch,
                                    IAzureDevOpsWorkItemPlugin azureDevOpsWorkItemPlugin,
                                    IChatClientProvider chatClientProvider)
    {
        _gitHubClient = gitHubClient.Client;
        _githubIssuePlugin = githubIssuePlugin;
        _azureDevOpsWorkItemPlugin = azureDevOpsWorkItemPlugin;
        _azureDevOpsSourceCodeSearch = azureDevOpsSourceCodeSearch;
        _logger = logger;
        _chatClientProvider = chatClientProvider;
    }

    public async Task<string> QueryRepositoryBasedOnError(string resourceId, string errorDescription)
    {
        try
        {
            string connectedRepositoryUrl = await _azureDevOpsWorkItemPlugin.FindConnectedRepository(resourceId);

            // If Azure DevOps repo is connected, use ADO search.
            if (!string.IsNullOrEmpty(connectedRepositoryUrl) && Regex.IsMatch(connectedRepositoryUrl, GraphService.AzDoRepoRegexPattern))
            {
                return await PerformAzureDevOpsSearch(connectedRepositoryUrl, errorDescription);
            }

            // Otherwise, default to GitHub search and let the errors be automatically handled.
            return await PerformGitHubSearch(resourceId, errorDescription, limitResults: true);
        }

        catch (Exception ex)
        {
            return $"Error occurred while querying repository: {ex.Message}";
        }
    }

    private void LogHttpResponse(HttpResponseMessage response, string apiName, string responseContent = "")
    {
        // Log response status
        _logger.LogInternalInformation("{ApiName} Response - Status Code: {StatusCode}", apiName, response.StatusCode);
        _logger.LogInternalInformation("{ApiName} Response - Reason Phrase: {ReasonPhrase}", apiName, response.ReasonPhrase);

        // Log response headers (excluding sensitive ones)
        _logger.LogInternalInformation("{ApiName} Response Headers:", apiName);
        foreach (var header in response.Headers)
        {
            var headerValue = SensitiveHeaders.Contains(header.Key) ? "[REDACTED]" : string.Join(", ", header.Value);
            _logger.LogInternalInformation("  {HeaderName}: {HeaderValue}", header.Key, headerValue);
        }

        // Log content headers if they exist (excluding sensitive ones)
        if (response.Content?.Headers != null)
        {
            _logger.LogInternalInformation("{ApiName} Content Headers:", apiName);
            foreach (var header in response.Content.Headers)
            {
                var headerValue = SensitiveHeaders.Contains(header.Key) ? "[REDACTED]" : string.Join(", ", header.Value);
                _logger.LogInternalInformation("  {HeaderName}: {HeaderValue}", header.Key, headerValue);
            }
        }

        // Log response content if provided
        if (!string.IsNullOrEmpty(responseContent))
        {
            _logger.LogInternalInformation("{ApiName} Response Content: {ResponseContent}", apiName, responseContent);
        }
    }

    public async Task<IReadOnlyList<SemanticSearchResult>> GetSemanticSearchResult(string resourceId, string query)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            throw new ArgumentException("Resource ID cannot be null or empty.", nameof(resourceId));
        }

        if (string.IsNullOrEmpty(query))
        {
            throw new ArgumentException("Query cannot be null or empty.", nameof(query));
        }

        // Find connected repository.
        string repoUrl = await _githubIssuePlugin.FindConnectedRepo(resourceId);
        if (string.IsNullOrEmpty(repoUrl))
        {
            string errorMessage = $"No connected repository found for resource ID: {resourceId}";
            _logger.LogInternalError(errorMessage);
            throw new ArgumentException(errorMessage);
        }

        // Ensure the repository is indexed by attempting indexing twice if necessary.
        var errors = new List<string>();
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var (isIndexed, indexError) = await IsRepositoryIndexedWithDetails(repoUrl);
            if (isIndexed)
            {
                break;
            }

            if (!string.IsNullOrEmpty(indexError))
            {
                errors.Add($"Index check attempt {attempt + 1}: {indexError}");
            }

            _logger.LogInternalInformation("Repository not indexed. Attempting to index: Attempt {AttemptNumber}", attempt + 1);
            var indexingResult = await ForceRepositoryIndexing(repoUrl);
            if (!indexingResult.Contains("successfully"))
            {
                errors.Add($"Indexing attempt {attempt + 1}: {indexingResult}");
            }

            // Add a delay to allow indexing to take effect before rechecking.
            await Task.Delay(5000);
        }

        var (finalIsIndexed, finalIndexError) = await IsRepositoryIndexedWithDetails(repoUrl);
        if (!finalIsIndexed)
        {
            if (!string.IsNullOrEmpty(finalIndexError))
            {
                errors.Add($"Final index check: {finalIndexError}");
            }
            string errorMessage = $"Failed to index repository after multiple attempts: {repoUrl}. Errors: {string.Join("; ", errors)}";
            _logger.LogInternalError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        // This method throws. 
        var (owner, repo) = GitHubHelper.ParseGitHubUrl(repoUrl);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/embeddings/code/search");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("MS-SRE-Agent", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-GitHub-Api-Version", "2024-05-14");
        string password = _gitHubClient.Credentials?.Password ?? throw new InvalidOperationException($"GitHub credentials are not set - please ensure the GitHub repository {repoUrl} is connected.");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", password);

        request.Content = new StringContent(JsonSerializer.Serialize(new SemanticSearchRequest
        {
            Prompt = query,
            ScopingQuery = $"repo:{owner}/{repo}",
            IncludeEmbeddings = false
        }), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            // Log all response details using helper method
            LogHttpResponse(response, "GitHub Embeddings API", responseString);

            response.EnsureSuccessStatusCode();

            var res = JsonSerializer.Deserialize<SemanticSearchResponse>(responseString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return res?.Results ?? new List<SemanticSearchResult>();
        }

        catch (Exception e)
        {
            _logger.LogInternalError(e, "Error fetching semantic search results from GitHub API", resourceId);
            throw;
        }
    }

    public async Task<(bool isIndexed, string error)> IsRepositoryIndexedWithDetails(string repoUrl)
    {
        (string owner, string repo) = GitHubHelper.ParseGitHubUrl(repoUrl);
        var endpoint = $"https://api.github.com/repos/{owner}/{repo}/copilot_internal/embeddings_index";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.UserAgent.ParseAdd("MS-SRE-Agent/1.0");
            string password = _gitHubClient.Credentials?.Password ?? throw new InvalidOperationException($"GitHub credentials are not set - please ensure the GitHub repository {repoUrl} is connected.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", password);
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Log all response details using helper method
            LogHttpResponse(response, "Repository Index Status API", responseContent);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var indexStatus = JsonSerializer.Deserialize<JsonElement>(responseContent);
                bool semanticCodeSearchOk = indexStatus.TryGetProperty("semantic_code_search_ok", out var searchOkProp) && searchOkProp.GetBoolean();
                return (semanticCodeSearchOk, string.Empty);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var error = "Repository not found or token does not have access (404 Not Found)";
                _logger.LogInternalWarning(error + ": {Endpoint}", endpoint);
                return (false, error);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                var error = "Access forbidden - this could be due to insufficient permissions or GitHub abuse rate limiting (403 Forbidden)";
                _logger.LogInternalWarning(error + ": {Endpoint}", endpoint);
                return (false, error);
            }
            else
            {
                var error = $"Unexpected status code {response.StatusCode}";
                _logger.LogInternalWarning(error + " from {Endpoint}", response.StatusCode, endpoint);
                return (false, error);
            }
        }
        catch (Exception ex)
        {
            var error = $"Error checking repository index status: {ex.Message}";
            _logger.LogInternalError(ex, "Error checking repository index status", repoUrl);
            return (false, error);
        }
    }

    public async Task<bool> IsRepositoryIndexed(string repoUrl)
    {
        var (isIndexed, _) = await IsRepositoryIndexedWithDetails(repoUrl);
        return isIndexed;
    }

    public async Task<string> ForceRepositoryIndexing(string repoUrl)
    {
        (string owner, string repo) = GitHubHelper.ParseGitHubUrl(repoUrl);

        string endpoint = $"https://api.github.com/repos/{owner}/{repo}/copilot_internal/embeddings_index";
        try
        {
            // GitHub API requires User-Agent and Authorization headers
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.UserAgent.ParseAdd("MS-SRE-Agent/1.0");
            string password = _gitHubClient.Credentials?.Password ?? throw new InvalidOperationException($"GitHub credentials are not set - please ensure the GitHub repository {repoUrl} is connected.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", password);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Log all response details using helper method
            LogHttpResponse(response, "Force Repository Indexing API", responseContent);

            var responseStatus = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Created => "Indexing task queued successfully (201 Created).",
                System.Net.HttpStatusCode.NotFound => "Repository not found or access denied (404 Not Found).",
                System.Net.HttpStatusCode.Forbidden => "Access forbidden - this could be due to insufficient permissions or GitHub abuse rate limiting (403 Forbidden).",
                System.Net.HttpStatusCode.ServiceUnavailable => "Auto indexing not allowed right now (503 Service Unavailable). Try again later.",
                _ => $"Unexpected response: {response.StatusCode}"
            };

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInternalInformation($"Indexing task queued successfully for repository: {repoUrl}");
                await Task.Delay(10_000); // Wait for 10 seconds to allow the task to be queued
            }
            else
            {
                _logger.LogInternalInformation("Failed to queue indexing task for repository {RepositoryId}: {ResponseStatus}", repoUrl, responseStatus);
            }

            return responseStatus;
        }

        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error forcing repository indexing", repoUrl);
            return $"Error occurred while forcing repository indexing: {ex.Message}";
        }
    }

    public async Task<string> QueryRepository(string resourceId, string query)
    {
        // Precondition checks
        if (string.IsNullOrEmpty(resourceId))
        {
            throw new ArgumentException("Resource ID cannot be null or empty.", nameof(resourceId));
        }

        if (string.IsNullOrEmpty(query))
        {
            throw new ArgumentException("Query cannot be null or empty.", nameof(query));
        }

        try
        {
            string azureDevOpsParsedUrl = await _azureDevOpsWorkItemPlugin.FindConnectedRepository(resourceId);

            // If Azure DevOps repo is connected, use ADO search.
            if (!string.IsNullOrEmpty(azureDevOpsParsedUrl) && Regex.IsMatch(azureDevOpsParsedUrl, GraphService.AzDoRepoRegexPattern))
            {
                return await PerformAzureDevOpsSearch(azureDevOpsParsedUrl, query);
            }

            return await PerformGitHubSearch(resourceId, query);
        }

        catch (Exception ex)
        {
            return $"Error occurred while querying repository: {ex.Message}";
        }
    }

    /// <summary>
    /// Performs Azure DevOps source code search using AI-powered query transformation and semantic search.
    /// </summary>
    /// <param name="azureDevOpsParsedUrl">The Azure DevOps repository URL</param>
    /// <param name="query">The natural language query to search for</param>
    /// <returns>A formatted string containing the search results</returns>
    private async Task<string> PerformAzureDevOpsSearch(string azureDevOpsParsedUrl, string query)
    {
        (string organization, string project, string repo) = AzureDevOpsWorkItemPlugin.ParseRepositoryUrl(azureDevOpsParsedUrl);

        List<ChatMessage> messages = [
            new ChatMessage(ChatRole.System, SYSTEM_PROMPT),
            new ChatMessage(ChatRole.User, query)
        ];

        var chatResponse = await _chatClientProvider.DefaultModel.GetResponseAsync(messages);
        var searchQueries = chatResponse.Messages[0].Text?.Trim();

        var allResults = new List<string>();

        if (!string.IsNullOrEmpty(searchQueries))
        {
            List<string> queries;

            // Try to parse as JSON array first, otherwise treat as single query
            if (searchQueries.StartsWith("[") && searchQueries.EndsWith("]"))
            {
                try
                {
                    queries = JsonSerializer.Deserialize<List<string>>(searchQueries) ?? [searchQueries];
                }
                catch (JsonException ex)
                {
                    _logger.LogInternalWarning(ex, "Failed to parse search queries as JSON array, treating as single query");
                    queries = [searchQueries];
                }
            }
            else
            {
                queries = [searchQueries];
            }

            var processedFiles = new HashSet<string>();
            var fileContentCache = new Dictionary<string, string>();

            foreach (var searchQuery in queries)
            {
                var searchResult = await _azureDevOpsSourceCodeSearch.SearchAsync(repoUrl: azureDevOpsParsedUrl, searchTerm: searchQuery);
                if (searchResult != null && searchResult.IsSuccess && searchResult.Results != null && searchResult.Results.Count > 0)
                {
                    // Get the files for each of the search results.
                    foreach (var result in searchResult.Results)
                    {
                        // Skip if we've already processed this file
                        if (processedFiles.Contains(result.FilePath))
                        {
                            continue;
                        }

                        if (result.Matches != null && result.Matches.Count > 0)
                        {
                            // Fetch file content once and cache it
                            if (!fileContentCache.TryGetValue(result.FilePath, out var fileContent))
                            {
                                fileContent = await _azureDevOpsSourceCodeSearch.GetFileContentAsync(organization, project, result.RepositoryId, result.FilePath);
                                fileContentCache[result.FilePath] = fileContent;
                            }

                            // Add all matches for this file
                            var matchesText = string.Join("\n", result.Matches);
                            allResults.Add($"File: {result.FileName}\nMatches:\n{matchesText}\nContent:\n{fileContent}\n");
                            processedFiles.Add(result.FilePath);
                        }
                    }
                }
            }
        }

        if (allResults.Count == 0)
        {
            return "No search results found in Azure DevOps repository.";
        }

        return string.Join("\n---\n", allResults);
    }

    /// <summary>
    /// Performs GitHub semantic search including repository indexing and result formatting.
    /// </summary>
    /// <param name="resourceId">The resource ID to find the connected GitHub repository</param>
    /// <param name="query">The query to search for</param>
    /// <param name="limitResults">Whether to limit results to top 5 (default: false)</param>
    /// <returns>A formatted string containing the search results</returns>
    private async Task<string> PerformGitHubSearch(string resourceId, string query, bool limitResults = false)
    {
        var searchResults = await GetSemanticSearchResult(resourceId, query);

        if (searchResults is null || !searchResults.Any())
        {
            return "No search results found.";
        }

        var resultsToProcess = limitResults ? searchResults.Take(5) : searchResults;
        var resultStrings = resultsToProcess.Select(result =>
        {
            var filePath = result?.Location?.Path ?? "Unknown";
            var score = result?.Distance ?? 0.0;
            var content = result?.Chunk?.Text ?? "No content";
            var start = result?.Chunk?.Range?.Start.ToString() ?? "N/A";
            var end = result?.Chunk?.Range?.End.ToString() ?? "N/A";

            return $"File: {filePath}\nScore: {score:F2}\nContent: {content} at {start} to {end}\n";
        });

        return string.Join("\n---\n", resultStrings);
    }
}
