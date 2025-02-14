// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Octokit;

namespace Agent.Plugins
{
    public interface IGithubIssuePlugin
    {
        Task<Issue> CreateGithubIssue(string repoUrl, string title, string body, string[] tags);
        Task<IssueComment> CreateGithubIssueComment(string repoUrl, int number, string commentBody);
        Task DeleteGithubIssueComment(string repoUrl, long id, string newCommentBody);
        Task<IReadOnlyList<IssueComment>> FetchGithubIssueComments(string repoUrl, int issueNumber);
        Task<IEnumerable<GithubIssuePluginIssue>> FetchGithubIssues(string repoUrl, GithubIssuePluginIssueFilter issueFilter, GithubIssuePluginItemStateFilter itemStateFilter, string milestone = "none", string assignee = "none", string? creator = null, string? mentioned = null, string[]? labels = null, DateTimeOffset? since = null);
        Task<Issue> UpdateGithubIssue(string repoUrl, int number, string? newTitle = null, string? newBody = null, string[]? labelsToAdd = null, string[]? labelsToRemove = null, ItemState? newState = null);
        Task<IssueComment> UpdateGithubIssueComment(string repoUrl, long id, string newCommentBody);
    }
}