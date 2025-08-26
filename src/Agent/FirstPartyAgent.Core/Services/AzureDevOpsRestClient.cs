using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using FirstPartyAgent.Core.Configuration;
using Agent.Core.Configuration;
using Microsoft.Extensions.Hosting;

namespace FirstPartyAgent.Core.Services
{
    public interface IAzureDevOpsClient
    {
        Task<string> ListFilesAsync(string path, int topN, string recursionLevel = "OneLevel");
        Task<string> ReadFileAsync(string path, string branch = "master");
        Task<string> GetCommitHistoryAsync(int top = 10);
        Task<string> CreateBranchAsync(string sourceBranchName, string newBranchName);
        Task<string> CreateCommitAsync(string branchName, string filePath, string fileContent, string commitMessage);
        Task<string> CreatePullRequestAsync(string sourceBranchName, string targetBranchName, string title, string description = "");
        Task<string> AbandonPullRequestAsync(int pullRequestId);
        Task<string> SearchCodeAsync(string searchText, int topN);
        Task<string> QueryWorkItemsAsync(string wiqlQuery, string? organization = null, string? project = null);
        Task<string> GetWorkItemByIdAsync(int workItemId, string? organization = null, string? project = null);
        Task<string> CreateWorkItemAsync(string workItemType, string title, string? description = null, string? assignedTo = null, string? organization = null, string? project = null);
        Task<string> AssignWorkItemAsync(int workItemId, string assignedTo);
        string MainBranchName { get; }
    }

    public class NullableAzureDevOpsRestClient : IAzureDevOpsClient
    {
        public Task<string> ListFilesAsync(string path, int topN, string recursionLevel = "OneLevel") => Task.FromResult<string>("Azure DevOps Client is Disabled.");
        public Task<string> ReadFileAsync(string path, string branch = "master") => Task.FromResult<string>("Azure DevOps Client is Disabled.");
        public Task<string> GetCommitHistoryAsync(int top = 10) => Task.FromResult<string>("Azure DevOps Client is Disabled.");
        public Task<string> CreateBranchAsync(string sourceBranchName, string newBranchName) => Task.FromResult<string>("Azure DevOps Client is Disabled.");
        public Task<string> CreateCommitAsync(string branchName, string filePath, string fileContent, string commitMessage) => Task.FromResult<string>("Azure DevOps Client is Disabled.");
        public Task<string> CreatePullRequestAsync(string sourceBranchName, string targetBranchName, string title, string description = "") => Task.FromResult<string>("Azure DevOps Client is Disabled.");
        public Task<string> AbandonPullRequestAsync(int pullRequestId) => Task.FromResult<string>("Azure DevOps Client is Disabled.");
        public Task<string> SearchCodeAsync(string searchText, int topN) => Task.FromResult<string>("Azure DevOps Client is Disabled.");
        public Task<string> QueryWorkItemsAsync(string wiqlQuery, string? organization = null, string? project = null) => Task.FromResult<string>("Azure DevOps Client is Disabled.");
        public Task<string> GetWorkItemByIdAsync(int workItemId, string? organization = null, string? project = null) => Task.FromResult<string>("Azure DevOps Client is Disabled.");
        public Task<string> CreateWorkItemAsync(string workItemType, string title, string? description = null, string? assignedTo = null, string? organization = null, string? project = null) => Task.FromResult<string>("Azure DevOps Client is Disabled.");
        public Task<string> AssignWorkItemAsync(int workItemId, string assignedTo) => Task.FromResult<string>("Azure DevOps Client is Disabled.");
        public string MainBranchName => "main";
    }

    /// <summary>
    /// A client for interacting with Azure DevOps REST API.
    /// </summary>
    public class AzureDevOpsRestClient : IAzureDevOpsClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _organization;
        private readonly string _project;
        private readonly string _repositoryId;
        private readonly string _projectName;
        private readonly string _repositoryName;
        private readonly string _mainBranchName;
        private readonly string _endpoint;
        private readonly string _searchEndpoint;

        public string MainBranchName => _mainBranchName;

        public AzureDevOpsRestClient(IHostEnvironment environment, AzureDevOpsSettings devOpsSettings)
        {
            _endpoint = devOpsSettings.Endpoint;
            _searchEndpoint = devOpsSettings.SearchEndpoint;
            _organization = devOpsSettings.Organization;
            _project = devOpsSettings.ProjectId;
            _repositoryId = devOpsSettings.RepositoryId;
            _projectName = devOpsSettings.ProjectName;
            _repositoryName = devOpsSettings.RepositoryName;
            _mainBranchName = devOpsSettings.MainBranchName;

            string accessToken;
            if (!string.IsNullOrWhiteSpace(devOpsSettings.PersonalAccessToken))
            {
                accessToken = devOpsSettings.PersonalAccessToken;
            }
            else
            {
                var credentialOptions = (environment.IsDevelopment() || string.IsNullOrWhiteSpace(devOpsSettings.ManagedIdentityClientId))
                    ? new DefaultAzureCredentialOptions()
                    : new DefaultAzureCredentialOptions
                    {
                        ManagedIdentityClientId = devOpsSettings.ManagedIdentityClientId
                    };
                var credential = new DefaultAzureCredential(credentialOptions); // CodeQL [SM05137] This is non-production code which is deprecated and not deployed.
                var tokenRequestContext = new TokenRequestContext(new[] { devOpsSettings.TokenRequestContext });
                accessToken = credential.GetToken(tokenRequestContext, default).Token;
            }

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<string> ListFilesAsync(string path, int topN, string recursionLevel = "OneLevel")
        {
            var url = $"{_endpoint}{_organization}/{_project}/_apis/git/repositories/{_repositoryId}/items?scopePath={path}&recursionLevel={recursionLevel}&api-version=7.0";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
            return result;
        }

        public async Task<string> ReadFileAsync(string path, string branch = "master")
        {
            var url = $"{_endpoint}{_organization}/{_project}/_apis/git/repositories/{_repositoryId}/items" +
                      $"?path={Uri.EscapeDataString(path)}" +
                      $"&recursionLevel=0" +
                      $"&includeContentMetadata=true" +
                      $"&versionDescriptor.version={Uri.EscapeDataString(branch)}" +
                      $"&versionDescriptor.versionOptions=0" +
                      $"&versionDescriptor.versionType=0" +
                      $"&includeContent=true" +
                      $"&resolveLfs=true" +
                      $"&api-version=7.0";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            // Extract the "content" property from the JSON response
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("content", out var contentElement))
            {
                var content = contentElement.GetString();
                if (content is not null)
                    return content;
                else
                    throw new InvalidOperationException("File content is null in the response.");
            }
            else
            {
                throw new InvalidOperationException("File content not found in the response.");
            }
        }

        public async Task<string> GetCommitHistoryAsync(int top = 10)
        {
            var url = $"{_endpoint}{_organization}/{_project}/_apis/git/repositories/{_repositoryId}/commits?$top={top}&api-version=7.0";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> CreateBranchAsync(string sourceBranchName, string newBranchName)
        {
            // Get the latest commit objectId from the source branch
            var refsUrl = $"{_endpoint}{_organization}/{_project}/_apis/git/repositories/{_repositoryId}/refs?filter=heads/{sourceBranchName}&api-version=7.0";
            var refsResponse = await _httpClient.GetAsync(refsUrl);
            refsResponse.EnsureSuccessStatusCode();
            var refsContent = await refsResponse.Content.ReadAsStringAsync();
            var refsJson = JsonDocument.Parse(refsContent);

            var value = refsJson.RootElement.GetProperty("value");
            if (value.GetArrayLength() == 0)
                throw new InvalidOperationException($"Source branch '{sourceBranchName}' not found.");

            var objectId = value[0].GetProperty("objectId").GetString();

            // Prepare the new branch ref update
            var newBranch = new[]
            {
                new
                {
                    name = $"refs/heads/{newBranchName}",
                    oldObjectId = "0000000000000000000000000000000000000000", // Required for new branch
                    newObjectId = objectId
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(newBranch), Encoding.UTF8, "application/json");
            var createBranchUrl = $"{_endpoint}{_organization}/{_project}/_apis/git/repositories/{_repositoryId}/refs?api-version=7.0";
            var request = new HttpRequestMessage(HttpMethod.Post, createBranchUrl) { Content = content };
            var createBranchResponse = await _httpClient.SendAsync(request);
            createBranchResponse.EnsureSuccessStatusCode();
            return await createBranchResponse.Content.ReadAsStringAsync();
        }

        public async Task<string> CreateCommitAsync(string branchName, string filePath, string fileContent, string commitMessage)
        {
            // Get the latest commit objectId for the branch
            var refsUrl = $"{_endpoint}{_organization}/{_project}/_apis/git/repositories/{_repositoryId}/refs?filter=heads/{branchName}&api-version=7.0";
            var refsResponse = await _httpClient.GetAsync(refsUrl);
            refsResponse.EnsureSuccessStatusCode();
            var refsContent = await refsResponse.Content.ReadAsStringAsync();
            var refsJson = JsonDocument.Parse(refsContent);
            var value = refsJson.RootElement.GetProperty("value");
            if (value.GetArrayLength() == 0)
                throw new InvalidOperationException($"Branch '{branchName}' not found.");
            var objectId = value[0].GetProperty("objectId").GetString();

            // Check if the file exists
            var fileExists = false;
            var fileUrl = $"{_endpoint}{_organization}/{_project}/_apis/git/repositories/{_repositoryId}/items?path={filePath}&versionDescriptor.version={branchName}&api-version=7.0";
            var fileResponse = await _httpClient.GetAsync(fileUrl);
            if (fileResponse.IsSuccessStatusCode)
                fileExists = true;

            var changeType = fileExists ? "edit" : "add";

            // Prepare the new commit
            var newCommit = new
            {
                refUpdates = new[]
                {
                    new
                    {
                        name = $"refs/heads/{branchName}",
                        oldObjectId = objectId
                    }
                },
                commits = new[]
                {
                    new
                    {
                        comment = commitMessage,
                        changes = new[]
                        {
                            new
                            {
                                changeType = changeType,
                                item = new { path = filePath },
                                newContent = new
                                {
                                    content = fileContent,
                                    contentType = "rawtext"
                                }
                            }
                        }
                    }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(newCommit), Encoding.UTF8, "application/json");
            var pushUrl = $"{_endpoint}{_organization}/{_project}/_apis/git/repositories/{_repositoryId}/pushes?api-version=7.0";
            var pushResponse = await _httpClient.PostAsync(pushUrl, content);
            pushResponse.EnsureSuccessStatusCode();
            return await pushResponse.Content.ReadAsStringAsync();
        }
        public async Task<string> CreatePullRequestAsync(string sourceBranchName, string targetBranchName, string title, string description = "")
        {
            var createPrUrl = $"{_endpoint}{_organization}/{_project}/_apis/git/repositories/{_repositoryId}/pullrequests?api-version=7.0";

            var prPayload = new
            {
                sourceRefName = $"refs/heads/{sourceBranchName}",
                targetRefName = $"refs/heads/{targetBranchName}",
                title = title,
                description = description
            };

            var content = new StringContent(JsonSerializer.Serialize(prPayload), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, createPrUrl) { Content = content };
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        public async Task<string> AbandonPullRequestAsync(int pullRequestId)
        {
            var abandonPrUrl = $"{_endpoint}{_organization}/{_project}/_apis/git/repositories/{_repositoryId}/pullrequests/{pullRequestId}?api-version=7.0";

            var payload = new
            {
                status = "abandoned"
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Patch, abandonPrUrl) { Content = content };
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> SearchCodeAsync(string searchText, int topN)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                throw new ArgumentException("searchText must not be empty.", nameof(searchText));
            if (topN <= 0)
                throw new ArgumentException("topN must be a positive integer.", nameof(topN));

            var searchUrl = $"{_searchEndpoint}{_organization}/{_project}/_apis/search/codeAdvancedQueryResults?api-version=7.0-preview.1";
            var payload = new
            {
                searchText = searchText,
                skipResults = 0,
                takeResults = topN,
                sortOptions = new object[] { },
                summarizedHitCountsNeeded = true,
                searchFilters = new
                {
                    ProjectFilters = new[] { _projectName },
                    RepositoryFilters = new[] { _repositoryName }
                },
                filters = new object[] { },
                includeSuggestions = false
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, searchUrl) { Content = content };
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Code Search failed: {response.StatusCode} - {errorContent}");
            }

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> QueryWorkItemsAsync(string wiqlQuery, string? organization = null, string? project = null)
        {
            var url = $"{_endpoint}{organization ?? _organization}/{project ?? _project}/_apis/wit/wiql?api-version=7.0";
            var payload = new { query = wiqlQuery };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetWorkItemByIdAsync(int workItemId, string? organization = null, string? project = null)
        {
            var url = $"{_endpoint}{organization ?? _organization}/{project ?? _project}/_apis/wit/workitems/{workItemId}?api-version=7.0";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> CreateWorkItemAsync(string workItemType, string title, string? description = null, string? assignedTo = null, string? organization = null, string? project = null)
        {
            var url = $"{_endpoint}{organization ?? _organization}/{project ?? _project}/_apis/wit/workitems/{workItemType}?api-version=7.0";
            var patchDocument = new List<object>
            {
                new { op = "add", path = "/fields/System.Title", value = title }
            };

            if (!string.IsNullOrWhiteSpace(description))
            {
                patchDocument.Add(new { op = "add", path = "/fields/System.Description", value = description });
            }
            if (!string.IsNullOrWhiteSpace(assignedTo))
            {
                patchDocument.Add(new { op = "add", path = "/fields/System.AssignedTo", value = assignedTo });
            }

            var content = new StringContent(JsonSerializer.Serialize(patchDocument), Encoding.UTF8, "application/json-patch+json");
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            request.Headers.Add("Accept", "application/json");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> AssignWorkItemAsync(int workItemId, string assignedTo)
        {
            var url = $"{_endpoint}{_organization}/{_project}/_apis/wit/workitems/{workItemId}?api-version=7.0";
            var patchDocument = new[]
            {
                new { op = "add", path = "/fields/System.AssignedTo", value = assignedTo }
            };
            var content = new StringContent(JsonSerializer.Serialize(patchDocument), Encoding.UTF8, "application/json-patch+json");
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
            request.Headers.Add("Accept", "application/json");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }

}
