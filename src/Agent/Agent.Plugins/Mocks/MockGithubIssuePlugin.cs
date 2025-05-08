using Microsoft.SemanticKernel;
using Octokit;

namespace Agent.Plugins.Mocks;
public class MockGithubIssuePlugin : IGithubIssuePlugin
{
    private readonly List<string> _reposScanned;
    private readonly List<GithubIssuePluginDependabotVulnerability> _githubIssuePluginDependabotVulnerabilities;

    public Guid? ThreadId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public MockGithubIssuePlugin()
    {
        _reposScanned = new List<string>();
    }

    public MockGithubIssuePlugin(List<GithubIssuePluginDependabotVulnerability> githubIssuePluginDependabotVulnerabilities)
        : this()
    {
        _githubIssuePluginDependabotVulnerabilities = githubIssuePluginDependabotVulnerabilities;
    }

    public string GenerateLoginLink()
    {
        return string.Empty;
    }

    public Task<Issue> CreateGithubIssue(string repoUrl, string title, string body, string[] tags)
    {
        throw new NotImplementedException();
    }

    public Task<IssueComment> CreateGithubIssueComment(string repoUrl, int number, string commentBody)
    {
        throw new NotImplementedException();
    }

    public Task DeleteGithubIssueComment(string repoUrl, long id, string newCommentBody)
    {
        throw new NotImplementedException();
    }

    public Task<GithubIssuePluginIssue> FetchGithubIssue(string issueUrl, Kernel kernel)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<GithubIssuePluginIssueComment>> FetchGithubIssueComments(string repoUrl, int issueNumber, Kernel kernel)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<GithubIssuePluginIssue>> FetchGithubIssues(string repoUrl, GithubIssuePluginIssueFilter issueFilter, GithubIssuePluginItemStateFilter itemStateFilter, string milestone = "none", string assignee = "none", string? creator = null, string? mentioned = null, string[]? labels = null, DateTimeOffset? since = null)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<GithubIssuePluginDependabotVulnerability>> FetchGithubSecurityDependabotAlerts(string repoUrl)
    {
        _reposScanned.Add(repoUrl);

        if (_githubIssuePluginDependabotVulnerabilities == null)
        {
            return Task.FromResult(Enumerable.Empty<GithubIssuePluginDependabotVulnerability>());
        }

        return Task.FromResult(_githubIssuePluginDependabotVulnerabilities.AsEnumerable());
    }

    public Task<IEnumerable<string>> GetUserOrganizations(string username)
    {
        throw new NotImplementedException();
    }

    public Task<Issue> UpdateGithubIssue(string repoUrl, int number, string? newTitle = null, string? newBody = null, string[]? labelsToAdd = null, string[]? labelsToRemove = null, ItemState? newState = null)
    {
        throw new NotImplementedException();
    }

    public Task<IssueComment> UpdateGithubIssueComment(string repoUrl, long id, string newCommentBody)
    {
        throw new NotImplementedException();
    }

    public List<string> GetReposScanned()
    {
        return _reposScanned;
    }

    public Task<string> ExtractTextFromImageInGitHubIssue(string imageUrl, Kernel kernel)
    {
        throw new NotImplementedException();
    }

    public Task<string> FindConnectedRepo(string resourceId)
    {
        throw new NotImplementedException();
    }
}
