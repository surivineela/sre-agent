// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Plugins.Helpers;
using Agent.Plugins.Interface;
using Microsoft.SemanticKernel;
using Octokit;

namespace Agent.Plugins;

[AgentToolPlugin(Category = ToolCategories.DevOps)]
[Description(@"Note that pull requests are considered issues.
You can create/ update comments on a PR the same way you would on a regular issue.
Note that if there is any auth issue when using any of these methods, call the GenerateLoginLink method and ask the user to follow this link to login")]
public class GitHubIssuePluginDefinition
{
    private readonly IGithubIssuePlugin _gitHubIssuePlugin;
    private readonly Kernel _kernel;

    public GitHubIssuePluginDefinition(IGithubIssuePlugin githubIssuePlugin, Kernel kernel)
    {
        _gitHubIssuePlugin = githubIssuePlugin;
        _kernel = kernel;
    }

    [KernelFunction("create_github_issue")]
    [Description("Create an issue on GitHub to track a problem with a web app which you have diagnosed if you have a solution to fix it. Unless this is a sample issue, make the publisher be detailed. If the user requests to set something that isn't supported, let them know. If there are any credential related issues when executing this plugin, call generate_login_link and ask the user to follow a link to login. Note: Assignees are validated to ensure they are real GitHub users before assignment.")]
    public async Task<Issue> CreateGithubIssue(
        [Description($"GitHub repository URL, e.g. {GitHubHelper.ExampleUrl}")] string repoUrl,
        [Description("Title of issue")] string title,
        [Description("Body of issue")] string body,
        [Description("Tags to put on issue")] string[] tags,
        [Description("GitHub usernames to assign to the issue (optional). Only valid GitHub users will be assigned. 'copilot' will be automatically transformed to 'copilot-swe-agent[bot]'.")] string[]? assignees = null
    )
    {
        return await _gitHubIssuePlugin.CreateGithubIssue(repoUrl, title, body, tags, assignees);
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

    [KernelFunction("fetch_github_issue")]
    [Description("Fetch a specific github issue. If the returned object is empty and is not an exception, let the user know there were none found.")]
    public async Task<GithubIssuePluginIssue> FetchGithubIssue(
            [Description("Github issue URL, e.g. https://github.com/owner/repo-name/issues/issueNumber")] string issueUrl
        )
    {
        return await _gitHubIssuePlugin.FetchGithubIssue(issueUrl, _kernel);
    }

    [KernelFunction("fetch_github_security_dependabot_alert")]
    [Description("Fetches all dependabot issues for a github repo. If the returned object is empty and is not an exception, let the user know there were none found.")]
    public async Task<IEnumerable<GithubIssuePluginDependabotVulnerability>> FetchGithubSecurityDependabotAlerts(
            [Description("GitHub repository URL, e.g. https://github.com/owner/repo-name")] string repoUrl
        )

    {
        return await _gitHubIssuePlugin.FetchGithubSecurityDependabotAlerts(repoUrl);
    }

    [KernelFunction("fetch_github_issue_comments")]
    [Description(@"Fetch comments for a specific github issue.")]
    public async Task<IReadOnlyList<GithubIssuePluginIssueComment>> FetchGithubIssueComments(
        [Description($"GitHub repository URL, e.g. {GitHubHelper.ExampleUrl}")] string repoUrl,
        int issueNumber
    )
    {
        return await _gitHubIssuePlugin.FetchGithubIssueComments(repoUrl, issueNumber, _kernel);
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

    [KernelFunction("get_user_organizations")]
    [Description("Get the names of all organizations a GitHub user is part of.")]
    public async Task<IEnumerable<string>> GetUserOrganizations(
            [Description("GitHub username")] string username
        )
    {
        return await _gitHubIssuePlugin.GetUserOrganizations(username);
    }

    [KernelFunction("extract_text_from_image_in_github_issue")]
    [Description("Extract text from an image in a GitHub issue body or comment. The image URL is of the form https://github.com/user-attachments/assets/GUID.")]
    public async Task<string> ExtractTextFromImageInGitHubIssue(
        [Description("URL of the image in body of issue or comment. Must be of the form https://github.com/user-attachments/assets/GUID.")] string imageUrl,
        Kernel kernel)
    {
        return await _gitHubIssuePlugin.ExtractTextFromImageInGitHubIssue(imageUrl, kernel);
    }

    [KernelFunction("find_connected_repo")]
    [Description("Find the GitHub repository URL where source code for an Azure resource like webapp, container app, aks pod etc is hosted. This helps identify the correct repository for creating GitHub issues related to code problems such as memory leaks, deadlocks, performance issues, or bugs discovered in Azure resources. The function uses a graph database to trace the relationship between deployed resources and their source code repositories.")]
    public async Task<string> FindConnectedGitHubRepo(
    [Description("The Azure resource ID for which to find the connected repository. Must be in the format '/subscriptions/{subId}/resourceGroups/{rgName}/providers/{provider}/{resourceType}/{resourceName}' or a unique identifier for the resource in your environment.")] string resourceId)
    {
        return await _gitHubIssuePlugin.FindConnectedRepo(resourceId);
    }

    [Description("Disconnects or unlinks an Azure Resource from a Github repository. For example: 'Disconnect the albumapicsharp-2 app from the connected Github repository' or 'Unlink the memory-leak-app app from the from the connected github repository'.")]
    public async Task<string> DisconnectRepositoryFromResourceForGitHub([Description("The resource ID of the Azure Resource for example: /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourceGroups/mrsharm-operations-agent-3p-rg/providers/Microsoft.App/containerApps/memory-leak-app/containerapp")] string resourceId)
    {
        return await _gitHubIssuePlugin.DisconnectRepository(resourceId);
    }

    [Description("Gets the type of Infrastructure as Code (IaC) - this is the most likely type of IaC used.")] 
    public async Task<string> GetIaCForGitHub(
        [Description("GitHub repository URL, e.g. https://github.com/owner/repo-name.git")] string repoUrl,
        [Description("Branch - assume main unless otherwise specified.")] string branch = "main",
        [Description("Comma separated file patterns to match for retrieving files (e.g. '*.bicep,*.json')")] string fileMatches = "*bicep,*yaml,*yml,*json,*tf*")
    {
        return await _gitHubIssuePlugin.GetIaCForGithub(repoUrl, branch, fileMatches);
    }
}
