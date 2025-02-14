// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Plugins.Helpers;
using Microsoft.SemanticKernel;
using Octokit;

namespace Agent.Plugins;

[Description(@"Note that pull requests are considered issues.
You can create/ update comments on a PR the same way you would on a regular issue.")]

public class GitHubIssuePluginDefinition
{
    private readonly IGithubIssuePlugin _gitHubIssuePlugin;

    public GitHubIssuePluginDefinition(IGithubIssuePlugin githubIssuePlugin)
    {
        _gitHubIssuePlugin = githubIssuePlugin;
    }

    [KernelFunction("create_github_issue")]
    [Description("Create an issue on GitHub to track a problem with a web app which you have diagnosed if you have a solution to fix it. Unless this is a sample issue, make the publisher be detailed. If the user requests to set something that isn't supported, let them know.")]
    public async Task<Issue> CreateGithubIssue(
        [Description($"GitHub repository URL, e.g. {GitHubHelper.ExampleUrl}")] string repoUrl,
        [Description("Title of issue")] string title,
        [Description("Body of issue")] string body,
        [Description("Tags to put on issue")] string[] tags
    )
    {
        return await _gitHubIssuePlugin.CreateGithubIssue(repoUrl, title, body, tags);
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
        [Description($"GitHub repository URL, e.g. {GitHubHelper.ExampleUrl}")] string repoUrl,
        [Description("Required: The unique number of a single github issue")] int number,
        string commentBody
    )
    {
        return await _gitHubIssuePlugin.CreateGithubIssueComment(repoUrl, number, commentBody);
    }

    [KernelFunction("update_github_issue")]
    [Description("Update a github issue. If the user requests to update something that isn't supported, let them know.")]
    public async Task<Issue> UpdateGithubIssue(
        [Description($"GitHub repository URL, e.g. {GitHubHelper.ExampleUrl}")] string repoUrl,
        [Description("Required: The unique number of a single github issue")] int number,
        string? newTitle = null,
        string? newBody = null,
        string[]? labelsToAdd = null,
        string[]? labelsToRemove = null,
        ItemState? newState = null
    )
    {
        return await _gitHubIssuePlugin.UpdateGithubIssue(repoUrl, number, newTitle, newBody, labelsToAdd, labelsToRemove, newState);
    }

    [KernelFunction("update_github_issue_comment")]
    [Description("Update a github issue comment.")]
    public async Task<IssueComment> UpdateGithubIssueComment(
        [Description($"GitHub repository URL, e.g. {GitHubHelper.ExampleUrl}")] string repoUrl,
        [Description("Required: The unique id of a single github issue comment. You can fetch this from a link to the comment if you need to.")] long id,
        string newCommentBody
    )
    {
        return await _gitHubIssuePlugin.UpdateGithubIssueComment(repoUrl, id, newCommentBody);
    }

    [KernelFunction("fetch_github_issues")]
    [Description("Fetch github issues. If the returned object is empty and is not an exception, just let the user know there were none found. If there are more than 3 issues matching, prompt the user to be more specific instead of returning all.")]
    public async Task<IEnumerable<GithubIssuePluginIssue>> FetchGithubIssues(
        [Description("GitHub repository URL, e.g. https://github.com/owner/repo-name.git")] string repoUrl,
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
        return await _gitHubIssuePlugin.FetchGithubIssues(repoUrl, issueFilter, itemStateFilter, milestone, assignee, creator, mentioned, labels, since);
    }

    [KernelFunction("fetch_github_issue_comments")]
    [Description(@"Fetch comments for a specific github issue.")]
    public async Task<IReadOnlyList<IssueComment>> FetchGithubIssueComments(
        [Description($"GitHub repository URL, e.g. {GitHubHelper.ExampleUrl}")] string repoUrl,
        int issueNumber
    )
    {
        return await _gitHubIssuePlugin.FetchGithubIssueComments(repoUrl, issueNumber);
    }

    [KernelFunction("delete_github_issue_comment")]
    [Description("Delete a github issue comment.")]
    public async Task DeleteGithubIssueComment(
        [Description($"GitHub repository URL, e.g. {GitHubHelper.ExampleUrl}")] string repoUrl,
        [Description("Required: The unique id of a single github issue comment. You can fetch this from a link to the comment if you need to.")] long id,
        string newCommentBody
    )
    {
        await _gitHubIssuePlugin.DeleteGithubIssueComment(repoUrl, id, newCommentBody);
    }
}

