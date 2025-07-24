using System.ComponentModel;
using System.Text.RegularExpressions;
using Agent.Core.Models;
using Agent.Plugins.Services;

namespace Agent.Plugins.Definitions;
public enum RepositoryType
{
    GitHub,
    AzureDevOps,
    Unknown
}

[AgentToolPlugin(Category = ToolCategories.DevOps)]
public class RepositoryPluginDefintion
{
    [Description("Based on a URL of a repository, this tool checks if a repository is a GitHub repository or an Azure DevOps repository. It returns the type of the repository as either 'GitHub' or 'AzureDevOps'. Use this tool to determine the type of a repository based on its URL.")]
    public Task<RepositoryType> GetRepositoryType([Description("The repository URL to check, for example: ")] string repositoryUrl)
    {
        if (Regex.IsMatch(repositoryUrl, GraphService.GithubRepoRegexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return Task.FromResult(RepositoryType.GitHub);
        }

        if (Regex.IsMatch(repositoryUrl, GraphService.AzDoRepoRegexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return Task.FromResult(RepositoryType.AzureDevOps);
        }

        return Task.FromResult(RepositoryType.Unknown);
    }
}
