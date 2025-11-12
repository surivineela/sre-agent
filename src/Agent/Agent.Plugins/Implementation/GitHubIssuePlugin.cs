// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Plugins.Helpers;
using Agent.Plugins.Interface;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json;
using Octokit;

namespace Agent.Plugins;

public class GitHubIssuePlugin : IGithubIssuePlugin
{
    private const string AGENT_ID = nameof(GitHubIssuePlugin);
    private readonly ILogger<GitHubIssuePlugin> _logger;
    private readonly GitHubSettings _gitHubSettings;
    private Octokit.GitHubClient _gitHubClient;
    private readonly IThreadRepository _threadRepository;
    private readonly IGraphDatabaseClient _graphDatabaseClient;
    public Guid? ThreadId { get; set; }

    public GitHubIssuePlugin(IThreadRepository threadRepository,
        GitHubSettings gitHubSettings,
        IGraphDatabaseClient graphDatabaseClient,
        ILogger<GitHubIssuePlugin> logger, Models.GitHubClient gitHubClient)
    {
        _logger = logger;
        _gitHubSettings = gitHubSettings;

        _graphDatabaseClient = graphDatabaseClient;

        _gitHubClient = gitHubClient.Client;
        if (!string.IsNullOrEmpty(_gitHubSettings.PatTokenOverride))
        {
            _gitHubClient.Credentials = new Credentials(token: _gitHubSettings.PatTokenOverride, authenticationType: AuthenticationType.Bearer);
        }

        _threadRepository = threadRepository;
    }

    public async Task<Issue> CreateGithubIssue(
        string repoUrl,
        string title,
        string body,
        string[] tags,
        string[]? assignees = null
    )
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GitHubIssuePlugin),
            async () =>
            {
                var (owner, repo) = GitHubHelper.ParseGitHubUrl(repoUrl);

                StringBuilder newBody = new(body);

                newBody.AppendLine();
                newBody.AppendLine("---");
                string agentName = Environment.GetEnvironmentVariable("AGENT_NAME") ?? "SRE Agent";
                newBody.AppendLine($"*This issue was created by {agentName}*");

                string agentDeepLink = GenerateThreadLink(this.ThreadId?.ToString() ?? string.Empty);
                newBody.AppendLine($"Tracked by the SRE agent [here]({agentDeepLink})");

                var issue = new NewIssue(title)
                {
                    Body = newBody.ToString()
                };

                foreach (var tag in tags)
                {
                    issue.Labels.Add(tag);
                }

                if (assignees != null)
                {
                    // Validate assignees exist before adding them
                    var validatedAssignees = await ValidateGitHubUsersAsync(assignees);
                    foreach (var assignee in validatedAssignees)
                    {
                        issue.Assignees.Add(assignee);
                    }
                }

                return await SendGitHubCallAsync(() => _gitHubClient.Issue.Create(owner, repo, issue));
            },
            _logger
        );
    }

    public async Task<string> FindConnectedRepo(string resourceId)
    {
        try
        {
            if (string.IsNullOrEmpty(resourceId))
            {
                _logger.LogInternalError("Resource ID cannot be null or empty");
                throw new ArgumentException("Resource ID cannot be null or empty", nameof(resourceId));
            }

            if (!ResourceIdentifier.TryParse(resourceId, out _))
            {
                _logger.LogInternalError("Not a valid resource id, must be in form /subscriptions/<>/resourceGroups/<>/providers/<provider-name>/<resource-type>/<resource-name>");
                throw new ArgumentException("Resource ID cannot be null or empty", nameof(resourceId));
            }
            var resourceNodeId = resourceId.ToLower().Replace("/", "_");
            string nodeExistsQuery = $"g.V().hasId('{resourceNodeId}').has('isDeleted', false)";
            var nodeExistsResults = await _graphDatabaseClient.Query(nodeExistsQuery);

            if (nodeExistsResults == null || !nodeExistsResults.Any())
            {
                _logger.LogInternalInformation($"Resource node not found for resource ID: {resourceId}");
                throw new Exception($"Resource with ID '{resourceId}' not found in the graph database. Please verify the resource exists.");
            }

            string query = $"g.V().hasId('{resourceNodeId}').has('isDeleted', false).outE('{Constants.Relationships.ServesCode}').inV().has('isDeleted', false).values('resourceId')";

            var results = await _graphDatabaseClient.Query<string>(query);

            if (results == null || !results.Any())
            {
                _logger.LogInternalInformation($"No connected repository found for resource ID: {resourceId}");
                throw new Exception($"No GitHub repository is connected to resource '{resourceId}'. Please link a repository using the LinkSourceCode functionality first.");
            }

            string repoUrl = results.FirstOrDefault()?.ToString() ?? string.Empty;

            if (string.IsNullOrEmpty(repoUrl))
            {
                _logger.LogInternalError($"Found empty repository URL for resource ID: {resourceId}");
                throw new Exception($"Empty GitHub repository URL found for resource '{resourceId}'. The link may be corrupted.");
            }

            _logger.LogInternalInformation($"Found connected repository: {repoUrl} for resource ID: {resourceId}");
            return repoUrl;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error finding connected repository for resource ID: {resourceId}");
            throw;
        }
    }

    public async Task<IssueComment> CreateGithubIssueComment(
        string repoUrl,
        int number,
        string commentBody
    )
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GitHubIssuePlugin),
            async () =>
            {
                var (owner, repo) = GitHubHelper.ParseGitHubUrl(repoUrl);

                return await SendGitHubCallAsync(() => _gitHubClient.Issue.Comment.Create(owner, repo, number, commentBody));
            },
            _logger
        );
    }

    public async Task<Issue> UpdateGithubIssue(
        string repoUrl,
        int number,
        string? newTitle = null,
        string? newBody = null,
        string[]? labelsToAdd = null,
        string[]? labelsToRemove = null,
        ItemState? newState = null
    )
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GitHubIssuePlugin),
            async () =>
            {
                var (owner, repo) = GitHubHelper.ParseGitHubUrl(repoUrl);

                Issue issue = await SendGitHubCallAsync(() => _gitHubClient.Issue.Get(owner, repo, number));
                var update = issue.ToUpdate();

                update.Title = newTitle ?? issue.Title;
                update.Body = newBody ?? issue.Body;
                update.State = newState ?? issue.State.Value;

                foreach (var label in labelsToAdd ?? Array.Empty<string>())
                {
                    update.AddLabel(label);
                }

                foreach (var label in labelsToRemove ?? Array.Empty<string>())
                {
                    update.RemoveLabel(label);
                }

                return await SendGitHubCallAsync(() => _gitHubClient.Issue.Update(owner, repo, number, update));
            },
            _logger
        );
    }

    public async Task<IssueComment> UpdateGithubIssueComment(
        string repoUrl,
        long id,
        string newCommentBody
    )
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GitHubIssuePlugin),
            async () =>
            {
                var (owner, repo) = GitHubHelper.ParseGitHubUrl(repoUrl);
                return await SendGitHubCallAsync(() => _gitHubClient.Issue.Comment.Update(owner, repo, id, newCommentBody));
            },
            _logger
        );
    }

    public async Task<IEnumerable<GithubIssuePluginIssue>> FetchGithubIssues(
        string repoUrl,
        GithubIssuePluginIssueFilter issueFilter,
        GithubIssuePluginItemStateFilter itemStateFilter,
        string milestone = "none",
        string assignee = "none",
        string? creator = null,
        string? mentioned = null,
        string[]? labels = null,
        DateTimeOffset? since = null
    )
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GitHubIssuePlugin),
            async () =>
            {
                var (owner, repo) = GitHubHelper.ParseGitHubUrl(repoUrl);

                var actualFilter = new RepositoryIssueRequest();

                actualFilter.Filter = (IssueFilter)issueFilter;
                actualFilter.State = (ItemStateFilter)itemStateFilter;
                actualFilter.Milestone = milestone;
                actualFilter.Assignee = assignee;
                actualFilter.Creator = creator;
                actualFilter.Mentioned = mentioned;
                actualFilter.Since = since;

                foreach (string label in labels ?? Array.Empty<string>())
                {
                    actualFilter.Labels.Add(label);
                }

                var res = await SendGitHubCallAsync(() => _gitHubClient.Issue.GetAllForRepository(owner, repo, actualFilter));

                _logger.LogInternalInformation($"Github issues fetched");

                // Only fetch issues, not pull requests
                return res.Where(issue => issue.PullRequest == null).Select(issue => issue.ToGithubIssuePluginIssue());
            },
            _logger
        );
    }

    public async Task<GithubIssuePluginIssue> FetchGithubIssue(
        string issueUrl,
        Kernel kernel
    )
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GitHubIssuePlugin),
            async () =>
            {
                var (owner, repo, issueNumber) = GitHubHelper.ParseGitHubIssueUrl(issueUrl);

                Issue res = await SendGitHubCallAsync(() => _gitHubClient.Issue.Get(owner, repo, issueNumber));

                _logger.LogInternalInformation($"GitHub issue with id {issueNumber} fetched from repo {owner}/{repo}");

                if (res == null)
                {
                    return default;
                }

                var issue = res.ToGithubIssuePluginIssue();
                if (!string.IsNullOrWhiteSpace(issue.Body))
                {
                    var enrichedContent = await EnrichContentText(issue.Body, kernel, kernel.GetRequiredService<IChatCompletionService>());
                    issue.Body = enrichedContent;
                    _logger.LogInternalInformation($"GitHub issue with id {issueNumber} enriched by extracting text from image");
                }

                return issue;
            },
            _logger
        );
    }

    public async Task<IEnumerable<GithubIssuePluginDependabotVulnerability>> FetchGithubSecurityDependabotAlerts(
        string repoUrl
    )
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GitHubIssuePlugin),
            async () =>
            {
                var (owner, repo) = GitHubHelper.ParseGitHubUrl(repoUrl);

                var endpoint = new Uri($"repos/{owner}/{repo}/dependabot/alerts", UriKind.Relative);
                var response = await SendGitHubCallAsync(() => _gitHubClient.Connection.Get<string>(endpoint, null, "application/vnd.github+json"));
                var responseBody = response.HttpResponse.Body?.ToString();
                if (string.IsNullOrEmpty(responseBody))
                {
                    return new List<GithubIssuePluginDependabotVulnerability>();
                }
                var responseObject = JsonConvert.DeserializeObject<DependabotAlert[]>(responseBody);

                var dependabotAlerts = new List<DependabotAlert>(responseObject ?? new DependabotAlert[0]);
                return dependabotAlerts.Select(
                    alert => new GithubIssuePluginDependabotVulnerability(
                        alert.Number,
                        alert.State,
                        alert.SecurityAdvisory.Summary ?? string.Empty,
                        alert.SecurityAdvisory.Description ?? string.Empty
                    ));
            },
            _logger
        );
    }

    public async Task<IReadOnlyList<GithubIssuePluginIssueComment>> FetchGithubIssueComments(
        string repoUrl,
        int issueNumber,
        Kernel kernel
        )
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GitHubIssuePlugin),
            async () =>
            {
                var (owner, repo) = GitHubHelper.ParseGitHubUrl(repoUrl);
                var pluginIssueComments = new List<GithubIssuePluginIssueComment>();
                try
                {
                    var comments = await SendGitHubCallAsync(() => _gitHubClient.Issue.Comment.GetAllForIssue(owner, repo, issueNumber));

                    pluginIssueComments = comments.Where(c => !string.IsNullOrWhiteSpace(c?.Body)).Select(c => c.ToGithubIssuePluginIssueComment()).ToList();

                    var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

                    var enrichedContentTasks = new List<Task<string>>(); // List to hold tasks for each comment
                    for (int c = 0; c < pluginIssueComments.Count(); c++)
                    {
                        var enrichedContentTask = EnrichContentText(pluginIssueComments[c].Body, kernel, chatCompletionService);
                        enrichedContentTasks.Add(enrichedContentTask);
                    }

                    // Wait for all tasks to complete
                    if (enrichedContentTasks.Count > 0)
                    {
                        var enrichedContents = await Task.WhenAll(enrichedContentTasks);
                        // Replace the image URL in content with imageURL+\n\n+imageDescription
                        for (int i = 0; i < pluginIssueComments.Count; i++)
                        {
                            var clonedComment = pluginIssueComments[i].DeepClone();
                            clonedComment.Body = enrichedContents[i];
                            pluginIssueComments[i] = clonedComment;
                        }
                    }

                    return pluginIssueComments;
                }
                catch (NotFoundException)
                {
                    return new List<GithubIssuePluginIssueComment>();
                }
            },
            _logger
        );
    }

    public string GenerateLoginLink()
    {
        string agentName = Environment.GetEnvironmentVariable("AGENT_NAME") ?? "agent";
        string agentHostname = Environment.GetEnvironmentVariable("AGENT_ENDPOINT") ?? "localhost";
        if (agentHostname.StartsWith("https://"))
        {
            agentHostname = agentHostname.Substring(8);
        }

        string agentNameHash = string.Empty;
        using (SHA256 sha256 = SHA256.Create())
        {
            var contentBytes = Encoding.UTF8.GetBytes(agentName);
            sha256.TransformFinalBlock(contentBytes, 0, contentBytes.Length);
            agentNameHash = sha256.Hash != null ? Convert.ToHexString(sha256.Hash).ToLower() : string.Empty;
        }

        var redirectUri = _gitHubSettings.RedirectUriFormat
            .Replace("{agentName}", agentName)
            .Replace("{agentHostname}", agentHostname)
            .Replace("?", "%3F")
            .Replace("=", "%3D")
            .Replace("&", "%26");

        return $"https://github.com/login/oauth/authorize?client_id={_gitHubSettings.ClientId}&redirect_uri={redirectUri}&scope=repo&prompt=consent&state={agentNameHash}";
    }

    public async Task DeleteGithubIssueComment(
        string repoUrl,
        long id,
        string newCommentBody
    )
    {
        await KernelFunctionHelpers.TryAction(
            nameof(GitHubIssuePlugin),
            async () =>
            {
                var (owner, repo) = GitHubHelper.ParseGitHubUrl(repoUrl);
                await SendGitHubCallAsync(() => _gitHubClient.Issue.Comment.Delete(owner, repo, id));
            },
            _logger
        );
    }

    public async Task<IEnumerable<string>> GetUserOrganizations(
        string username
    )
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GitHubIssuePlugin),
            async () =>
            {
                var organizations = await _gitHubClient.Organization.GetAllForUser(username);
                return organizations?.Select(org => org.Login) ?? new List<string>();
            },
            _logger
        );
    }

    public async Task<string> ExtractTextFromImageInGitHubIssue(string imageUrl, Kernel kernel)
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GitHubIssuePlugin),
            async () =>
            {
                if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith("https://github.com/user-attachments/assets/"))
                {
                    throw new ArgumentException("Invalid image url. Url must be of the form https://github.com/user-attachments/assets/GUID");
                }

                var imageUri = new Uri(imageUrl);
                var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
                var imageDescription = await ExtractImageDescription(imageUri, kernel, chatCompletionService);
                _logger.LogInternalInformation($"Image URL: {imageUrl}\nExtracted image description: {imageDescription}");
                return imageDescription;
            },
            _logger);
    }

    private async Task<string> EnrichContentText(string content, Kernel kernel, IChatCompletionService chatCompletionService)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }
        string enrichedContent = content;
        var embededImageUrls = GitHubHelper.GetEmbededImageUrls(content);

        var imageDescriptionTasks = new List<Task<string>>();
        foreach (var imageUrl in embededImageUrls)
        {
            var imageDescriptionTask = ExtractImageDescription(new Uri(imageUrl), kernel, chatCompletionService);
            imageDescriptionTasks.Add(imageDescriptionTask);
        }

        if (imageDescriptionTasks.Count > 0)
        {
            var imageDescriptions = await Task.WhenAll(imageDescriptionTasks);

            // Replace the image URL in content with imageURL and it's description
            for (int i = 0; i < embededImageUrls.Count; i++)
            {
                enrichedContent = enrichedContent.Replace($"{embededImageUrls[i]})", $"{embededImageUrls[i]})\n=========Image description\n{imageDescriptions[i]}\n=========\n");
            }
        }

        return enrichedContent;
    }

    private async Task<string> ExtractImageDescription(Uri imageUri, Kernel kernel, IChatCompletionService chatCompletionService)
    {
        if (imageUri == null || !imageUri.ToString().StartsWith("https://github.com/user-attachments/assets/"))
        {
            throw new ArgumentException("Invalid image URI. URI must be of the form https://github.com/user-attachments/assets/GUID");
        }

        var imageExtractionHistory = new ChatHistory();
        imageExtractionHistory.AddSystemMessage("You are an AI assistant that describes images accurately and concisely. Focus on technical details if the image contains code, error messages, or technical content.");

        var message = new ChatMessageContentItemCollection
                        {
                            new TextContent("Please extract the text from the image"),
                            new ImageContent(imageUri)
                        };

        imageExtractionHistory.AddUserMessage(message);

        try
        {
            ChatMessageContent result = await chatCompletionService.GetChatMessageContentAsync(
            imageExtractionHistory,
            executionSettings: new()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.None()
            },
            kernel: kernel);

            return result?.Content ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error extracting image description");
            return string.Empty;
        }
    }

    private async Task<string[]> ValidateGitHubUsersAsync(string[] usernames)
    {
        var validUsers = new List<string>();

        foreach (var username in usernames)
        {
            if (string.IsNullOrWhiteSpace(username))
                continue;

            // Transform "copilot" to "copilot-swe-agent[bot]"
            var actualUsername = username.Trim().ToLowerInvariant() == "copilot"
                ? "copilot-swe-agent[bot]"
                : username.Trim();

            try
            {
                // Try to get user information to validate existence
                var user = await SendGitHubCallAsync(() => _gitHubClient.User.Get(actualUsername));
                if (user != null)
                {
                    validUsers.Add(actualUsername);
                    if (actualUsername != username)
                    {
                        _logger.LogInternalInformation($"Transformed assignee '{username}' to '{actualUsername}' and validated GitHub user.");
                    }
                    else
                    {
                        _logger.LogInternalInformation($"Validated GitHub user: {actualUsername}");
                    }
                }
            }
            catch (Octokit.NotFoundException)
            {
                _logger.LogInternalWarning($"GitHub user '{actualUsername}' not found. Skipping assignment.");
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error validating GitHub user '{actualUsername}'. Skipping assignment.");
            }
        }

        if (validUsers.Count != usernames.Length)
        {
            var invalidUsers = usernames.Except(validUsers).ToArray();
            _logger.LogInternalWarning($"Some assignees were invalid and skipped: {string.Join(", ", invalidUsers)}");
        }

        return validUsers.ToArray();
    }

    private async Task SendGitHubCallAsync(Func<Task> githubCallFunc)
    {
        try
        {
            if (string.IsNullOrEmpty(_gitHubSettings.PatTokenOverride))
            {
                var token = await _threadRepository.GetGitHubAccessTokenAsync();
                if (token == null)
                {
                    throw new Exception($"User must login to this link: {GenerateLoginLink()}");
                }

                _gitHubClient.Credentials = new Credentials(token: token.AccessToken, authenticationType: AuthenticationType.Bearer);
            }
            else
            {
                _gitHubClient.Credentials = new Credentials(token: _gitHubSettings.PatTokenOverride, authenticationType: AuthenticationType.Bearer);
            }

            await githubCallFunc();
        }
        catch (Octokit.NotFoundException)
        {
            throw new Exception($"User must login to this link: {GenerateLoginLink()}");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error sending GitHub call");
            throw;
        }
    }

    private string GenerateThreadLink(string threadId)
    {
        var agentHost = "https://portal.azure.com/";
        var subscriptionId = Environment.GetEnvironmentVariable("AGENT_SUBSCRIPTION_ID") ?? string.Empty;
        var resourceGroup = Environment.GetEnvironmentVariable("AGENT_RESOURCE_GROUP") ?? string.Empty;
        var agentName = Environment.GetEnvironmentVariable("AGENT_NAME") ?? string.Empty;
        if (agentName.Contains("--"))
        {
            agentName = agentName.Substring(0, agentName.LastIndexOf("--"));
        }

        var queryString = "?feature.customPortal=false&feature.canmodifystamps=true&feature.fastmanifest=false&nocdn=force&websitesextension_loglevel=verbose&Microsoft_Azure_PaasServerless=beta&microsoft_azure_paasserverless_assettypeoptions=%7B%22SreAgentCustomMenu%22%3A%7B%22options%22%3A%22%22%7D%7D";

        var resourcePath = $"%2Fsubscriptions%2F{subscriptionId}%2FresourceGroups%2F{resourceGroup}%2Fproviders%2FMicrosoft.App%2Fagents%2F{agentName}";
        var deepLinkPath = $"%2Fviews%2Factivities%2Fthreads%2F{threadId}";
        var hash = $"#view/Microsoft_Azure_PaasServerless/AgentFrameBlade.ReactView/id/{resourcePath}/sreLink/{deepLinkPath}";

        return $"{agentHost}{queryString}{hash}";
    }

    private async Task<T> SendGitHubCallAsync<T>(Func<Task<T>> githubCallFunc)
    {
        try
        {
            if (string.IsNullOrEmpty(_gitHubSettings.PatTokenOverride))
            {
                var token = await _threadRepository.GetGitHubAccessTokenAsync();
                if (token == null)
                {
                    throw new Exception($"User is unauthenticated. Please login to this link: {GenerateLoginLink()}");
                }

                _gitHubClient.Credentials = new Credentials(token: token.AccessToken, authenticationType: AuthenticationType.Bearer);
            }
            else
            {
                _gitHubClient.Credentials = new Credentials(token: _gitHubSettings.PatTokenOverride, authenticationType: AuthenticationType.Bearer);
            }

            return await githubCallFunc();
        }
        catch (Octokit.NotFoundException)
        {
            throw new Exception($"The requested resource is not found. User must login to this link: {GenerateLoginLink()}");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error sending GitHub call");
            throw;
        }
    }

    public async Task<Dictionary<string, string>> GetFilesFromRepo(string repoUrl, string branch = "main", string fileMatches = "*bicep")
    {
        // Based on the Repo and branch, get the Git Tree for the code.
        var (owner, repo) = GitHubHelper.ParseGitHubUrl(repoUrl);
        var branchInfo = await _gitHubClient.Repository.Branch.Get(owner, repo, branch);
        var tree = await _gitHubClient.Git.Tree.GetRecursive(owner, repo, branchInfo.Commit.Sha);

        // Helper method to match on a particular file pattern.
        bool IsWildcardMatch(string path, string pattern)
        {
            string fileName = Path.GetFileName(path);

            if (pattern.StartsWith("*"))
            {
                string suffix = pattern.Substring(1);
                return fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
            }

            else if (pattern.EndsWith("*"))
            {
                string prefix = pattern.Substring(0, pattern.Length - 1);
                return fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            else if (pattern.Contains("*"))
            {
                string[] parts = pattern.Split('*');
                return fileName.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase) &&
                       fileName.EndsWith(parts[1], StringComparison.OrdinalIgnoreCase);
            }

            return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        // Filter blobs by file patterns using wildcard matching.
        IEnumerable<string> matches = fileMatches.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(pattern => pattern.Trim())
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern));

        var filteredBlobs = tree.Tree
            .Where(t => t.Type == TreeType.Blob && matches.Any(pattern => IsWildcardMatch(t.Path, pattern)));

        if (filteredBlobs == null || !filteredBlobs.Any())
        {
            _logger.LogInternalInformation($"No files matching '{fileMatches}' found in repository {repoUrl} on branch {branch}");
            return new Dictionary<string, string>();
        }

        Dictionary<string, string> fileNameToContent = new Dictionary<string, string>();

        foreach (var blob in filteredBlobs)
        {
            try
            {
                // download raw content by ref
                var content = await _gitHubClient.Repository.Content
                    .GetRawContentByRef(owner, repo, blob.Path, branch);
                fileNameToContent[blob.Path] = Encoding.UTF8.GetString(content);
                _logger.LogInternalInformation($"Downloaded: {blob.Path}");
            }

            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error downloading {blob.Path}");
            }
        }

        return fileNameToContent;
    }

    public async Task<string> GetIaCForGithub(string repoUrl, string branch = "main", string fileMatches = "*bicep")
    {
        var (owner, repo) = GitHubHelper.ParseGitHubUrl(repoUrl);

        // Step 1. Check the embeddings API for any existing IaC type for the repoUrl and branch.
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _gitHubClient.Credentials.Password);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitHubEmbeddingSearchClient", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2024-05-14");

        // Create a valid request
        var requestBody = new SemanticSearchRequest
        {
            Prompt = "List files that define infrastructure for Azure using Bicep, Terraform (azurerm), Pulumi (Azure), ARM templates, or Helm charts.",
            ScopingQuery = $"repo:{owner}/{repo}",
            IncludeEmbeddings = false,
            //Limit = 10,
            //EmbeddingModel = "text-embedding-3-small-512"
        };

        string json = System.Text.Json.JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            // Endpoint from the OpenAPI spec
            var response = await client.PostAsync("https://api.github.com/embeddings/code/search", content);
            var responseString = await response.Content.ReadAsStringAsync();
            var res = System.Text.Json.JsonSerializer.Deserialize<SemanticSearchResponse>(responseString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // First check if the response is valid.
            if (res != null && res!.Results != null && res.Results.Count > 0 && res.Results[0].Distance >= 0.5)
            {
                var detectedAzureIaCSystem = DetectAzureIaCSystem(res);
                return detectedAzureIaCSystem;
            }

            else
            {
                var iacTypeFromFiles = await GetIaCTypeFromFiles(repoUrl, branch, fileMatches);
                if (!string.IsNullOrEmpty(iacTypeFromFiles))
                {
                    return iacTypeFromFiles;
                }

                else
                {
                    return "No IaC Detected";
                }
            }
        }

        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error: {ex.Message}");
        }

        // Step 2. If not found, call GetFilesFromRepo to get the files and then use the IaC type detection logic to determine the IaC type.

        var iacTypeFromFilesCheck = await GetIaCTypeFromFiles(repoUrl, branch, fileMatches);
        if (!string.IsNullOrEmpty(iacTypeFromFilesCheck))
        {
            return iacTypeFromFilesCheck;
        }

        else
        {
            return "No IaC Detected";
        }
    }

    private static readonly Dictionary<string, string[]> IaCSignatures = new Dictionary<string, string[]>
    {
        { "Bicep", new[] { ".bicep" } },
        { "ARM", new[] { "schema.management.azure.com" } },
        { "Terraform", new[] { ".tf", ".tfvars", "provider \"azurerm\"", "resource \"azurerm_" } },
        { "Pulumi", new[] { "pulumi.yaml", "using pulumi.azurenative", "@pulumi/azure-native" } },
        { "Helm", new[] { "chart.yaml", "/templates/", "values.yaml", "kind: deployment", "kind: service" } }
    };

    public static string DetectAzureIaCSystem(SemanticSearchResponse embeddingResult)
    {
        var score = new Dictionary<string, (int, string)>(StringComparer.OrdinalIgnoreCase);

        if (embeddingResult?.Results == null)
        {
            return "No recognizable Azure IaC system detected.";
        }

        foreach (var result in embeddingResult.Results)
        {
            string fileName = result?.Location?.Path?.ToLower() ?? string.Empty;
            string content = result?.Chunk?.Text?.ToLower() ?? string.Empty;

            foreach (var kvp in IaCSignatures)
            {
                foreach (var signature in kvp.Value)
                {
                    if (fileName.Contains(signature) || content.Contains(signature))
                    {
                        if (!score.ContainsKey(kvp.Key))
                            score[kvp.Key] = (0, content);
                        score[kvp.Key] = (score[kvp.Key].Item1 + 1, score[kvp.Key].Item2);
                    }
                }
            }
        }

        if (score.Count == 0)
        {
            return "No recognizable Azure IaC system detected.";
        }

        var mostLikely = score.OrderByDescending(kvp => kvp.Value.Item1).First();
        return $"Most likely Azure IaC system generated using the Github embeddings api: {mostLikely.Key} (score: {mostLikely.Value.Item1}) - content to use: {mostLikely.Value.Item2}";
    }

    private async Task<string> GetIaCTypeFromFiles(string repoUrl, string branch = "main", string fileMatches = "*bicep,*yaml,*yml,*json,*tf*")
    {
        Dictionary<string, string> StaticFileMap = new(StringComparer.OrdinalIgnoreCase)
        {
            [".bicep"] = "Bicep",
            [".tf"] = "Terraform",
            [".tf.json"] = "Terraform",
        };

        Dictionary<string, string> detectedTools = new();

        // Get the files from the repo
        Dictionary<string, string> filesContent = await GetFilesFromRepo(repoUrl, branch, fileMatches);
        List<string> files = filesContent.Keys.ToList();

        foreach (var f in filesContent)
        {
            string fileName = f.Key;
            string fileContent = f.Value;
            string ext = Path.GetExtension(fileName);

            // Detect Helm
            if (fileName.Equals("Chart.yaml", StringComparison.OrdinalIgnoreCase) ||
                fileContent.Contains($"{Path.DirectorySeparatorChar}templates{Path.DirectorySeparatorChar}"))
            {
                detectedTools.Add("Helm", fileContent);
            }

            // Static file map (Terraform, Bicep)
            if (StaticFileMap.TryGetValue(ext, out var tool))
            {
                if (tool == "Terraform" && fileContent.Contains("provider \"azurerm\""))
                    detectedTools.Add("Terraform (Azure)", fileContent);
                else if (tool == "Bicep")
                    detectedTools.Add("Bicep", fileContent);
            }

            // ARM Templates
            else if (ext is ".json")
            {
                if (fileContent.Contains("\"$schema\"") && fileContent.Contains("management.azure.com"))
                {
                    detectedTools.Add("ARM Template", fileContent);
                }
            }

            // Ansible (Azure)
            else if (ext is ".yaml" or ".yml")
            {
                if (Regex.IsMatch(fileContent, @"azure_rm_|community\.azure"))
                {
                    detectedTools.Add("Ansible (Azure)", fileContent);
                }
            }

            // Pulumi - C#
            else if (ext is ".cs")
            {
                if (fileContent.Contains("Pulumi") && fileContent.Contains("Azure"))
                {
                    detectedTools.Add("Pulumi (C#)", fileContent);
                }
            }

            // Pulumi - TypeScript/JavaScript
            else if (ext is ".ts" or ".js")
            {
                if (Regex.IsMatch(fileContent, @"@pulumi/azure", RegexOptions.IgnoreCase))
                {
                    detectedTools.Add("Pulumi (TypeScript/JavaScript)", fileContent);
                }
            }

            // Pulumi - Python
            else if (ext is ".py")
            {
                if (Regex.IsMatch(fileContent, @"import pulumi_azure", RegexOptions.IgnoreCase))
                {
                    detectedTools.Add("Pulumi (Python)", fileContent);
                }
            }
        }

        if (detectedTools.Count > 0)
        {
            return string.Join(" \n\n", detectedTools.Select(kvp => $"Generated through file grepping: {kvp.Key}: {kvp.Value}"));
        }

        else
        {
            return "No IaC tools found";
        }
    }

    public async Task<string> DisconnectRepository(string resourceId)
    {
        var resourceNodeId = resourceId.ToLower().Replace("/", "_");
        string result = await _graphDatabaseClient.SoftDeleteConnectedRepositoryByResourceId(resourceNodeId);
        return result;
    }

    public async Task<IEnumerable<GithubIssuePluginIssue>> FetchGithubIssuesLimited(
        string repoUrl,
        int limit = 10,
        GithubIssuePluginItemStateFilter state = GithubIssuePluginItemStateFilter.Open
    )
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GitHubIssuePlugin),
            async () =>
            {
                var (owner, repo) = GitHubHelper.ParseGitHubUrl(repoUrl);

                var request = new RepositoryIssueRequest
                {
                    Filter = IssueFilter.All,
                    State = (ItemStateFilter)state
                };

                var options = new ApiOptions
                {
                    PageSize = Math.Min(limit, 100), // GitHub API max page size is 100
                    PageCount = 1
                };

                var issues = await SendGitHubCallAsync(() => _gitHubClient.Issue.GetAllForRepository(owner, repo, request, options));

                _logger.LogInternalInformation($"Fetched {issues.Count} GitHub issues (limited to {limit}) from {owner}/{repo}");

                // Only fetch issues, not pull requests, and limit the results
                return issues
                    .Where(issue => issue.PullRequest == null)
                    .Take(limit)
                    .Select(issue => issue.ToGithubIssuePluginIssue());
            },
            _logger
        );
    }
}

public class SemanticSearchResponse
{
    [JsonPropertyName("results")]
    public List<SemanticSearchResult>? Results { get; set; }

    [JsonPropertyName("embedding_model")]
    public string? EmbeddingModel { get; set; }
}

public class SemanticSearchResult
{
    [JsonPropertyName("location")]
    public LocationInfo? Location { get; set; }

    [JsonPropertyName("distance")]
    public float Distance { get; set; }

    [JsonPropertyName("chunk")]
    public Chunk? Chunk { get; set; }
}

public class LocationInfo
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("repo")]
    public Repository? Repo { get; set; }

    [JsonPropertyName("commit_sha")]
    public string? CommitSha { get; set; }

    [JsonPropertyName("ref_name")]
    public string? RefName { get; set; }

    [JsonPropertyName("language")]
    public Language? Language { get; set; }
}

public class Repository
{
    [JsonPropertyName("id")]
    public ulong Id { get; set; }

    [JsonPropertyName("nwo")]
    public string? Nwo { get; set; }

    [JsonPropertyName("owner_id")]
    public ulong OwnerId { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public class Language
{
    [JsonPropertyName("id")]
    public uint Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }
}

public class Chunk
{
    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("range")]
    public Range? Range { get; set; }

    [JsonPropertyName("line_range")]
    public Range? LineRange { get; set; }

    [JsonPropertyName("embedding")]
    public Embedding? Embedding { get; set; }
}

public class Range
{
    [JsonPropertyName("start")]
    public uint Start { get; set; }

    [JsonPropertyName("end")]
    public uint End { get; set; }
}

public class Embedding
{
    [JsonPropertyName("embedding")]
    public List<float>? Values { get; set; }
}

public class SemanticSearchRequest
{
    [JsonPropertyName("prompt")]
    public string? Prompt { get; set; }

    [JsonPropertyName("scoping_query")]
    public string? ScopingQuery { get; set; }

    [JsonPropertyName("include_embeddings")]
    public bool? IncludeEmbeddings { get; set; }

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    [JsonPropertyName("experiments")]
    public Dictionary<string, string>? Experiments { get; set; }

    [JsonPropertyName("embedding_model")]
    public string? EmbeddingModel { get; set; }
}


public struct DependabotAlert
{
    public long Id { get; set; }
    public int Number { get; set; }
    public string State { get; set; }
    public Dependency Dependency { get; set; }
    [JsonProperty("security_advisory")]
    public SecurityAdvisory SecurityAdvisory { get; set; }
    [JsonProperty("security_vulnerability")]
    public SecurityVulnerability SecurityVulnerability { get; set; }
    public string[] VulnerableManifestPaths { get; set; }
    public string VulnerableRequirements { get; set; }
    public User? DismissedBy { get; set; }
    public DateTimeOffset? DismissedAt { get; set; }
    public string DismissedReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? FixedAt { get; set; }
}

public struct Dependency
{
    public Package Package { get; set; }
    public string ManifestPath { get; set; }
    public string Scope { get; set; }
}

public struct Package
{
    public string Ecosystem { get; set; }
    public string Name { get; set; }
}

public struct SecurityAdvisory
{
    public string GhsaId { get; set; }
    public string CveId { get; set; }
    public string Summary { get; set; }
    public string Description { get; set; }
    public string Severity { get; set; }
    public Reference[] References { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? WithdrawnAt { get; set; }
}

public struct Reference
{
    public string Url { get; set; }
}

public struct SecurityVulnerability
{
    public Package Package { get; set; }
    public string Severity { get; set; }
    public string VulnerableVersionRange { get; set; }
    public string FirstPatchedVersion { get; set; }
}

public struct User
{
    public string Login { get; set; }
    public long Id { get; set; }
    public string AvatarUrl { get; set; }
    public string Url { get; set; }
}

public record struct GithubIssuePluginIssue(
    long Id,
    int Number,
    string Url,
    string State,
    string Title,
    string Body,
    string[] Labels,
    string? Assignee,
    string[] Assignees,
    GithubIssuePluginMilestone? Milestone,
    GithubIssuePluginPullRequest? PullRequest,
    DateTimeOffset? ClosedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

public record struct GithubIssuePluginDependabotVulnerability(
    int Number,
    string State,
    string Title,
    string Body
);

public record struct GithubIssuePluginMilestone(
    long Id,
    int Number,
    string State,
    string Title,
    string Description
);

public record struct GithubIssuePluginPullRequest(
    long Id,
    string Url,
    int Number,
    string State,
    string Title,
    string Body
);

public record struct GithubIssuePluginIssueRequest(
    GithubIssuePluginIssueFilter Filter,
    GithubIssuePluginItemStateFilter State,
    string Milestone,
    string Assignee,
    string? Creator,
    string? Mentioned,
    string[]? Labels
);

public record struct GithubIssuePluginIssueComment(
    long Id,
    string NodeId,
    string Url,
    string HtmlUrl,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Octokit.User User,
    ReactionSummary Reactions,
    StringEnum<AuthorAssociation> AuthorAssociation
    );

public enum GithubIssuePluginIssueFilter
{
    [Description("Issues assigned to the authenticated user")]
    Assigned,

    [Description("Issues created by the authenticated user")]
    Created,

    [Description("Issues mentioning the authenticated user")]
    Mentioned,

    [Description("Issues the authenticated user is subscribed to for updates")]
    Subscribed,

    [Description("All issues the authenticated user can see, regardless of participation or creation")]
    All
}

public enum GithubIssuePluginItemStateFilter
{
    Open,
    Closed,
    All
}

public static class GithubIssuePluginExtensions
{
    public static GithubIssuePluginIssue ToGithubIssuePluginIssue(this Issue issue)
    {
        return new GithubIssuePluginIssue(
             issue.Id,
             issue.Number,
             issue.Url,
             issue.State.StringValue,
             issue.Title,
             issue.Body,
             issue.Labels.Select(l => l.Name).ToArray(),
             issue.Assignee?.Login,
             issue.Assignees.Select(a => a.Login).ToArray(),
             issue.Milestone?.ToGithubIssuePluginMilestone(),
             issue.PullRequest?.ToGithubIssuePluginPullRequest(),
             issue.ClosedAt,
             issue.CreatedAt,
             issue.UpdatedAt
         );
    }

    public static GithubIssuePluginMilestone ToGithubIssuePluginMilestone(this Milestone milestone)
    {
        return new GithubIssuePluginMilestone(
            milestone.Id,
            milestone.Number,
            milestone.State.StringValue,
            milestone.Title,
            milestone.Description
        );
    }

    public static GithubIssuePluginPullRequest ToGithubIssuePluginPullRequest(this PullRequest pullRequest)
    {
        return new GithubIssuePluginPullRequest(
            pullRequest.Id,
            pullRequest.Url,
            pullRequest.Number,
            pullRequest.State.StringValue,
            pullRequest.Title,
            pullRequest.Body
        );
    }

    public static GithubIssuePluginIssueComment ToGithubIssuePluginIssueComment(this IssueComment comment)
    {
        // Map properties from IssueComment to GithubIssuePluginIssueComment
        return new GithubIssuePluginIssueComment(
            comment.Id,
            comment.NodeId,
            comment.Url,
            comment.HtmlUrl,
            comment.Body,
            comment.CreatedAt,
            comment.UpdatedAt,
            comment.User,
            comment.Reactions,
            comment.AuthorAssociation
        );
    }

    public static GithubIssuePluginIssueComment DeepClone(this GithubIssuePluginIssueComment comment)
    {
        // Map properties from IssueComment to GithubIssuePluginIssueComment
        return new GithubIssuePluginIssueComment(
            comment.Id,
            comment.NodeId,
            comment.Url,
            comment.HtmlUrl,
            comment.Body,
            comment.CreatedAt,
            comment.UpdatedAt,
            comment.User,
            comment.Reactions,
            comment.AuthorAssociation
        );
    }
}
