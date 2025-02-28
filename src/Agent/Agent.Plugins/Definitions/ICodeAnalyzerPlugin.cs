using Agent.Core.Helpers;
using Agent.Plugins.Models;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    public interface ICodeAnalyzerPlugin
    {
        Task<string> RequestGitHubTokenAsync(string userMessage, string repository);
        AsyncOperationStatusSummary<ManagedIdentityMigrationAnalysisDescriptor, string>? GetProcessRepositoryForManagedIdentityMigrationAndOpenPRStatus(
            Kernel kernel, string repoUrl, string branchToClone, string branchName, string sqlServer, string database);
        AsyncOperationStartResult<ManagedIdentityMigrationAnalysisDescriptor, string> ProcessRepositoryForManagedIdentityMigrationAndOpenPRAsync(
            Kernel kernel, string repoUrl, string branchToClone, string branchName, string sqlServer, string database);
        AsyncOperationStatusSummary<MemoryLeakeAnalysisDescriptor, string>? GetStatusAnalyzeAndFixMemoryLeaksAsync(
            string repoUrl, string baseBranch, string newBranch);
        AsyncOperationStartResult<MemoryLeakeAnalysisDescriptor, string> AnalyzeAndFixMemoryLeaksAsync(
            Kernel kernel, string repoUrl, string baseBranch, string newBranch, string memoryAnalysis);
        Task<IEnumerable<GithubIssuePluginIssue>> FetchGithubIssues(
            string repoUrl, GithubIssuePluginIssueFilter issueFilter, GithubIssuePluginItemStateFilter itemStateFilter,
            string milestone, string assignee, string? creator, string? mentioned, string[]? labels, DateTimeOffset? since);
    }
}
