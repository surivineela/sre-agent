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

        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitHubEmbeddingSearchClient", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2024-05-14");
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

        // This method throws. 
        var (owner, repo) = GitHubHelper.ParseGitHubUrl(repoUrl);

        // Step 1. Check the embeddings API for any existing IaC type for the repoUrl and branch.
        // Create a valid request
        var requestBody = new SemanticSearchRequest
        {
            Prompt = query,
            ScopingQuery = $"repo:{owner}/{repo}",
            IncludeEmbeddings = false,
            //Limit = 10,
            //EmbeddingModel = "text-embedding-3-small-512"
        };

        string json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            // Endpoint from the OpenAPI spec
            var response = await _httpClient.PostAsync("https://api.github.com/embeddings/code/search", content);
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
}
