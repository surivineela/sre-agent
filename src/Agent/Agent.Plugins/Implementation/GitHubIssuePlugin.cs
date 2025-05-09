// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Agent.Logging;
using Agent.Plugins.Helpers;
using Octokit;
using Agent.Core.Configuration;
using Newtonsoft.Json;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using Agent.Core.Interfaces;
using Agent.Graph.Crawler.ARM;
using Azure.Core;
using Agent.Data.DatabaseClients.GraphDbClient;

namespace Agent.Plugins;

public class GitHubIssuePlugin : IGithubIssuePlugin
{
    private const string AGENT_ID = nameof(GitHubIssuePlugin);
    private readonly ILogger<GitHubIssuePlugin> _logger;
    private readonly IConfiguration _config;
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
        _threadRepository = threadRepository;
    }

    public async Task<Issue> CreateGithubIssue(
        string repoUrl,
        string title,
        string body,
        string[] tags
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

                string agentDeepLink = GenerateThreadLink(this.ThreadId?.ToString());
                newBody.AppendLine($"Tracked by the SRE agent [here]({agentDeepLink})");

                var issue = new NewIssue(title)
                {
                    Body = newBody.ToString()
                };

                foreach (var tag in tags)
                {
                    issue.Labels.Add(tag);
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
            string nodeExistsQuery = $"g.V().hasId('{resourceNodeId}')";
            var nodeExistsResults = await _graphDatabaseClient.Query(nodeExistsQuery);

            if (nodeExistsResults == null || !nodeExistsResults.Any())
            {
                _logger.LogInternalInformation($"Resource node not found for resource ID: {resourceId}");
                throw new Exception($"Resource with ID '{resourceId}' not found in the graph database. Please verify the resource exists.");
            }

            string query = $"g.V().hasId('{resourceNodeId}').outE('{Constants.Relationships.ServesCode}').inV().values('resourceId')";

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
                var responseObject = JsonConvert.DeserializeObject<DependabotAlert[]>(response.HttpResponse.Body.ToString());

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

        var flags = string.Empty;
        flags += "&feature.customPortal=false&feature.canmodifystamps=true&feature.fastmanifest=false&nocdn=force&websitesextension_loglevel=verbose&Microsoft_Azure_PaasServerless=canary&microsoft_azure_paasserverless_assettypeoptions=%7B%22SreAgentCustomMenu%22%3A%7B%22options%22%3A%22%22%7D%7D";

        var resourcePath = $"%2Fsubscriptions%2F{subscriptionId}%2FresourceGroups%2F{resourceGroup}%2Fproviders%2FMicrosoft.App%2Fagents%2F{agentName}";

        return $"{agentHost}?Microsoft_Azure_PaasServerless_srelink=/views/activities/threads/{threadId}{flags}#view/Microsoft_Azure_PaasServerless/AgentFrameBlade/id/{resourcePath}";
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
                    throw new Exception($"User must login to this link: {GenerateLoginLink()}");
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
            throw new Exception($"User must login to this link: {GenerateLoginLink()}");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error sending GitHub call");
            throw;
        }
    }
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
