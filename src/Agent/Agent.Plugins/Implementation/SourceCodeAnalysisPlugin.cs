using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Agent.Plugins.Helpers;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;
using Octokit;

namespace Agent.Plugins.Implementation;

public class SourceCodeAnalysisPlugin : ISourceCodeAnalysisPlugin
{
    private readonly GitHubClient _gitHubClient;
    private readonly IGithubIssuePlugin _githubIssuePlugin;
    private readonly ILogger<SourceCodeAnalysisPlugin> _logger;
    private static readonly HttpClient _httpClient = new();

    public SourceCodeAnalysisPlugin(Models.GitHubClient gitHubClient, ILogger<SourceCodeAnalysisPlugin> logger, IGithubIssuePlugin githubIssuePlugin)
    {
        _gitHubClient = gitHubClient.Client;
        _githubIssuePlugin = githubIssuePlugin;
        _logger = logger;

        if (_gitHubClient.Credentials != null && !string.IsNullOrEmpty(_gitHubClient.Credentials.Password))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _gitHubClient.Credentials.Password);
        }
    }

    public async Task<string> QueryRepositoryBasedOnError(string resourceId, string errorDescription)
    {
        var searchResults = await GetSemanticSearchResult(resourceId, errorDescription);

        if (searchResults is null || !searchResults.Any())
        {
            return "No search results found.";
        }

        var top5Results = searchResults.Take(5);
        var resultStrings = top5Results.Select(result =>
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
        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (await IsRepositoryIndexed(repoUrl))
            {
                break;
            }

            _logger.LogInternalInformation("Repository not indexed. Attempting to index: Attempt {AttemptNumber}", attempt + 1);
            await ForceRepositoryIndexing(repoUrl);

            // Add a delay to allow indexing to take effect before rechecking.
            await Task.Delay(5000);
        }

        if (!await IsRepositoryIndexed(repoUrl))
        {
            string errorMessage = $"Failed to index repository after multiple attempts: {repoUrl}";
            _logger.LogInternalError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        // This method throws. 
        var (owner, repo) = GitHubHelper.ParseGitHubUrl(repoUrl);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/embeddings/code/search");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("GitHubEmbeddingSearchClient", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-GitHub-Api-Version", "2024-05-14");
        request.Content = new StringContent(JsonSerializer.Serialize(new SemanticSearchRequest
        {
            Prompt = query,
            ScopingQuery = $"repo:{owner}/{repo}",
            IncludeEmbeddings = false
        }), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
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

    public async Task<bool> IsRepositoryIndexed(string repoUrl)
    {
        (string owner, string repo) = GitHubHelper.ParseGitHubUrl(repoUrl);
        var endpoint = $"https://api.github.com/repos/{owner}/{repo}/copilot_internal/embeddings_index";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.UserAgent.ParseAdd("IndexingStatusClient/1.0");

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var indexStatus = JsonSerializer.Deserialize<JsonElement>(responseContent);
                bool semanticCodeSearchOk = indexStatus.TryGetProperty("semantic_code_search_ok", out var searchOkProp) && searchOkProp.GetBoolean();
                return semanticCodeSearchOk;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInternalWarning("Repository not found or token does not have access: {Endpoint}", endpoint);
                return false;
            }
            else
            {
                _logger.LogInternalWarning("Unexpected status code {StatusCode} from {Endpoint}", response.StatusCode, endpoint);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error checking repository index status", repoUrl);
            throw;
        }
    }

    public async Task<string> ForceRepositoryIndexing(string repoUrl)
    {
        (string owner, string repo) = GitHubHelper.ParseGitHubUrl(repoUrl);

        string endpoint = $"https://api.github.com/repos/{owner}/{repo}/copilot_internal/embeddings_index";
        try
        {
            // GitHub API requires User-Agent and Authorization headers
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.UserAgent.ParseAdd("RepositoryIndexer/1.0");

            var response = await _httpClient.SendAsync(request);
            var responseStatus = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Created => "Indexing task queued successfully (201 Created).",
                System.Net.HttpStatusCode.NotFound => "Repository not found or access denied (404 Not Found).",
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
            throw;
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

        var searchResults = await GetSemanticSearchResult(resourceId, query);

        if (searchResults is null || !searchResults.Any())
        {
            return "No search results found.";
        }

        var resultStrings = searchResults.Select(result =>
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
