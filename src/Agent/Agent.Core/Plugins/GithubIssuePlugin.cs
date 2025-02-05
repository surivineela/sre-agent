// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Octokit;

namespace Agent.Core.Plugins;

[Description(@"Note that pull requests are considered issues.
You can create/ update comments on a PR the same way you would on a regular issue.")]

public class GithubIssuePlugin
{
    private const string AGENT_ID = nameof(GithubIssuePlugin);
    private readonly ILogger<GithubIssuePlugin> _logger;
    private readonly IConfiguration _config;
    private readonly GitHubSettings _gitHubSettings;
    private Octokit.GitHubClient _gitHubClient;

    public GithubIssuePlugin(IConfiguration configuration, ILogger<GithubIssuePlugin> logger, Models.GitHubClient gitHubClient)
    {
        _config = configuration;
        _logger = logger;
        _gitHubSettings = configuration.GetSection("Azure").Get<AzureSettings>().Github;
        _gitHubClient = gitHubClient.Client;
    }

    [KernelFunction("create_github_issue")]
    [Description("Create an issue on GitHub to track a problem with a web app which you have diagnosed if you have a solution to fix it. Unless this is a sample issue, make the publisher be detailed. If the user requests to set something that isn't supported, let them know.")]
    public async Task<Issue> CreateGithubIssue(
        [Description("GitHub repository URL, e.g. https://github.com/owner/repo-name")] string repoUrl,
        [Description("Title of issue")] string title,
        [Description("Body of issue")] string body,
        [Description("Tags to put on issue")] string[] tags
    )
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GithubIssuePlugin),
            async () =>
            {
                var (owner, repo) = KernelFunctionHelpers.ParseGitHubUrl(repoUrl);

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

    [KernelFunction("create_github_issue_comment")]
    [Description(@"Create an comment on a GitHub issue or link a PR to an issue.
To link a PR to an issue, comment on the pull request.
You can comment on a PR the same way you would comment an issue, you just need to fetch them differently.

The following keywords auto close the issue when a linked PR is completed:
close
closes
closed
fix
fixes
fixed
resolve
resolves
resolved
")]
    public async Task<IssueComment> CreateGithubIssueComment(
        [Description("GitHub repository URL, e.g. https://github.com/owner/repo-name")] string repoUrl,
        [Description("Required: The unique number of a single github issue")] int number,
        string commentBody
    )
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GithubIssuePlugin),
            async () =>
            {
                var (owner, repo) = KernelFunctionHelpers.ParseGitHubUrl(repoUrl);

                return await _gitHubClient.Issue.Comment.Create(owner, repo, number, commentBody);
            },
            _logger
        );
    }

    [KernelFunction("update_github_issue")]
    [Description("Update a github issue. If the user requests to update something that isn't supported, let them know.")]
    public async Task<Issue> UpdateGithubIssue(
        [Description("GitHub repository URL, e.g. https://github.com/owner/repo-name")] string repoUrl,
        [Description("Required: The unique number of a single github issue")] int number,
        string? newTitle = null,
        string? newBody = null,
        string[]? labelsToAdd = null,
        string[]? labelsToRemove = null,
        ItemState? newState = null
    )
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GithubIssuePlugin),
            async () =>
            {
                var (owner, repo) = KernelFunctionHelpers.ParseGitHubUrl(repoUrl);

                Issue issue = await _gitHubClient.Issue.Get(owner, repo, number);
                var update = issue.ToUpdate();

                update.Title = newTitle ?? issue.Title;
                update.Body = newBody ?? issue.Body;
                update.State = newState ?? issue.State.Value;

                foreach (var label in labelsToAdd ?? Array.Empty<string>())
                {
                    update.Labels.Add(label);
                }

                foreach (var label in labelsToRemove ?? Array.Empty<string>())
                {
                    update.Labels.Remove(label);
                }

                return await _gitHubClient.Issue.Update(owner, repo, number, update);
            },
            _logger
        );
    }

    [KernelFunction("update_github_issue_comment")]
    [Description("Update a github issue comment.")]
    public async Task<IssueComment> UpdateGithubIssueComment(
        [Description("GitHub repository URL, e.g. https://github.com/owner/repo-name")] string repoUrl,
        [Description("Required: The unique id of a single github issue comment. You can fetch this from a link to the comment if you need to.")] long id,
        string newCommentBody
    )
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GithubIssuePlugin),
            async () =>
            {
                var (owner, repo) = KernelFunctionHelpers.ParseGitHubUrl(repoUrl);
                return await _gitHubClient.Issue.Comment.Update(owner, repo, id, newCommentBody);
            },
            _logger
        );
    }

    [KernelFunction("fetch_github_issues")]
    [Description("Fetch github issues. If the returned object is empty and is not an exception, just let the user know there were none found. If there are more than 3 issues matching, prompt the user to be more specific instead of returning all.")]
    public async Task<IEnumerable<GithubIssuePluginIssue>> FetchGithubIssues(
        [Description("GitHub repository URL, e.g. https://github.com/owner/repo-name")] string repoUrl,
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
            nameof(GithubIssuePlugin),
            async () =>
            {
                var (owner, repo) = KernelFunctionHelpers.ParseGitHubUrl(repoUrl);

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

    [KernelFunction("fetch_github_issue_comments")]
    [Description(@"Fetch comments for a specific github issue.")]
    public async Task<IReadOnlyList<IssueComment>> FetchGithubIssueComments(
        [Description("GitHub repository URL, e.g. https://github.com/owner/repo-name")] string repoUrl,
        int issueNumber
    )
    {
        return await KernelFunctionHelpers.TryAction(
            nameof(GithubIssuePlugin),
            async () =>
            {
                var (owner, repo) = KernelFunctionHelpers.ParseGitHubUrl(repoUrl);
                return await _gitHubClient.Issue.Comment.GetAllForIssue(owner, repo, issueNumber); ;
            },
            _logger
        );
    }

    [KernelFunction("delete_github_issue_comment")]
    [Description("Delete a github issue comment.")]
    public async Task DeleteGithubIssueComment(
        [Description("GitHub repository URL, e.g. https://github.com/owner/repo-name")] string repoUrl,
        [Description("Required: The unique id of a single github issue comment. You can fetch this from a link to the comment if you need to.")] long id,
        string newCommentBody
    )
    {
        await KernelFunctionHelpers.TryAction(
            nameof(GithubIssuePlugin),
            async () =>
            {
                var (owner, repo) = KernelFunctionHelpers.ParseGitHubUrl(repoUrl);
                await _gitHubClient.Issue.Comment.Delete(owner, repo, id);
            },
            _logger
        );
    }
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

