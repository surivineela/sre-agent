// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.SemanticKernel;
using Octokit;

namespace Agent.Plugins.Interface
{
    public interface IGithubIssuePlugin
    {
        public Guid? ThreadId { get; set; }

        Task<Issue> CreateGithubIssue(string repoUrl, string title, string body, string[] tags);
        Task<IssueComment> CreateGithubIssueComment(string repoUrl, int number, string commentBody);
        Task DeleteGithubIssueComment(string repoUrl, long id, string newCommentBody);
        Task<IReadOnlyList<GithubIssuePluginIssueComment>> FetchGithubIssueComments(string repoUrl, int issueNumber, Kernel kernel);
        Task<IEnumerable<GithubIssuePluginIssue>> FetchGithubIssues(string repoUrl, GithubIssuePluginIssueFilter issueFilter, GithubIssuePluginItemStateFilter itemStateFilter, string milestone = "none", string assignee = "none", string? creator = null, string? mentioned = null, string[]? labels = null, DateTimeOffset? since = null);
        Task<GithubIssuePluginIssue> FetchGithubIssue(string issueUrl, Kernel kernel);
        Task<IEnumerable<GithubIssuePluginDependabotVulnerability>> FetchGithubSecurityDependabotAlerts(string repoUrl);
        Task<Issue> UpdateGithubIssue(string repoUrl, int number, string? newTitle = null, string? newBody = null, string[]? labelsToAdd = null, string[]? labelsToRemove = null, ItemState? newState = null);
        Task<IssueComment> UpdateGithubIssueComment(string repoUrl, long id, string newCommentBody);
        Task<IEnumerable<string>> GetUserOrganizations(string username);
        Task<string> ExtractTextFromImageInGitHubIssue(string imageUrl, Kernel kernel);
        Task<string> FindConnectedRepo(string resourceId);
        Task<string> DisconnectRepository(string resourceId);
        Task<string> GetIaCForGithub(string repoUrl, string branch, string fileMatches);
        string GenerateLoginLink();
    }
}
