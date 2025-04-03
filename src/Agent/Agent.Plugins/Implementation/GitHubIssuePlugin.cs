// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Agent.Plugins.Helpers;
using Octokit;
using Agent.Core.Configuration;
using Newtonsoft.Json;

namespace Agent.Plugins;

public class GitHubIssuePlugin : IGithubIssuePlugin
{
    private const string AGENT_ID = nameof(GitHubIssuePlugin);
    private readonly ILogger<GitHubIssuePlugin> _logger;
    private readonly IConfiguration _config;
    private readonly GitHubSettings _gitHubSettings;
    private Octokit.GitHubClient _gitHubClient;

    public GitHubIssuePlugin(GitHubSettings gitHubSettings, ILogger<GitHubIssuePlugin> logger, Models.GitHubClient gitHubClient)
    {
        _logger = logger;
        _gitHubSettings = gitHubSettings;

        // TODO; Remove this post 3/31 demo
        string? ghToken = Environment.GetEnvironmentVariable("ghtoken");
        if (!string.IsNullOrEmpty(ghToken))
        {
            _logger.Log(LogLevel.Information, "Setting github token in GithubIssuePlugin");
            _gitHubSettings.PatOverride = ghToken;
        }

        _gitHubClient = gitHubClient.Client;
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

                var issue = new NewIssue(title)
                {
                    Body = body
                };

                foreach (var tag in tags)
                {
                    issue.Labels.Add(tag);
                }

                return await _gitHubClient.Issue.Create(owner, repo, issue);
            },
            _logger
        );
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

                return await _gitHubClient.Issue.Comment.Create(owner, repo, number, commentBody);
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

                Issue issue = await _gitHubClient.Issue.Get(owner, repo, number);
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

                return await _gitHubClient.Issue.Update(owner, repo, number, update);
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
                return await _gitHubClient.Issue.Comment.Update(owner, repo, id, newCommentBody);
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

                var res = await _gitHubClient.Issue.GetAllForRepository(owner, repo, actualFilter);

                _logger.LogInformation($"Github issues fetched");

                // Only fetch issues, not pull requests
                return res.Where(issue => issue.PullRequest == null).Select(issue => issue.ToGithubIssuePluginIssue());
            },
            _logger
        );
    }

    public async Task<GithubIssuePluginIssue> FetchGithubIssue(
        string issueUrl
    )
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GitHubIssuePlugin),
            async () =>
            {
                var (owner, repo, issueNumber) = GitHubHelper.ParseGitHubIssueUrl(issueUrl);

                var res = await _gitHubClient.Issue.Get(owner, repo, issueNumber);

                _logger.LogInformation($"GitHub issue with id {issueNumber} fetched from repo {owner}/{repo}");

                return res?.ToGithubIssuePluginIssue() ?? default;
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
                var response = await _gitHubClient.Connection.Get<string>(endpoint, null, "application/vnd.github+json");
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

    public async Task<IReadOnlyList<IssueComment>> FetchGithubIssueComments(
        string repoUrl,
        int issueNumber
    )
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GitHubIssuePlugin),
            async () =>
            {
                var (owner, repo) = GitHubHelper.ParseGitHubUrl(repoUrl);
                return await _gitHubClient.Issue.Comment.GetAllForIssue(owner, repo, issueNumber); ;
            },
            _logger
        );
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
                await _gitHubClient.Issue.Comment.Delete(owner, repo, id);
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
}

