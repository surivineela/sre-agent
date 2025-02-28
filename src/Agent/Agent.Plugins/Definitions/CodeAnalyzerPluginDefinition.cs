// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Plugins.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Agent.Plugins;

public class CodeAnalyzerPluginDefinition
{
    private readonly ICodeAnalyzerPlugin _codeAnalyzer;
    
    public CodeAnalyzerPluginDefinition(ICodeAnalyzerPlugin codeAnalyzer)
    {
        _codeAnalyzer = codeAnalyzer;
    }

    [KernelFunction("request_github_token_for_gh_actions")]
    [Description("Request GitHub token from user via Teams")]
    public async Task<string> RequestGitHubTokenAsync(
        [Description("Message to user posted regarding why Github login is needed")] string userMessage,
        [Description("Github full repository")] string repository)
    {
        return await _codeAnalyzer.RequestGitHubTokenAsync(
            userMessage: userMessage,
            repository: repository);
    }

    [KernelFunction("get_status_repo_for_mi_migration_2_adauth")]
    [Description("To get execution status of process_repo_for_mi_migration_2_adauth function. Returns null if not started yet.")]
    public AsyncOperationStatusSummary<ManagedIdentityMigrationAnalysisDescriptor, string>? GetProcessRepositoryForManagedIdentityMigrationAndOpenPRStatus(
        Kernel kernel,
        [Description("Full GitHub repository URL. Can be inferred from app being CI/CD Enabled. Always confirm. (Eg. https://github.com/sanchitehta/sample-app)")] string repoUrl,
        [Description("Name of the branch to clone. Can be inferred from app's CI?CD Branch")] string branchToClone,
        [Description("Name of the branch to create with the fix. Include 5 random alphanumeric chars at the end.")] string branchName,
        [Description("SQLServer name in the original connection string. We are trying to migrate this to to use AD Based auth")] string sqlServer,
        [Description("Database in the original connection string")] string database)
    {
        return GetProcessRepositoryForManagedIdentityMigrationAndOpenPRStatus(
            kernel: kernel,
            repoUrl: repoUrl,
            branchToClone: branchToClone,
            branchName: branchName,
            sqlServer: sqlServer,
            database: database);
    }

    [KernelFunction("process_repo_for_mi_mig_2_adauth")]
    [Description("Analyze code in a GitHub repo if app doesn't use MI based auth to SQL and open a PR with suggestions.\n" +
        "Should have retried the GH token before invoking this.\n" +
        "<category>Applicable if user asks for Identity SFI 1.6.2</category>\n" +
        "<IMPORTANT>ONLY OFFER THIS CAPABILITY TO THE USER IF check_sql_connection_type FINDS app is using connection string, always ask user for their repository details</IMPORTANT>")]
    public AsyncOperationStartResult<ManagedIdentityMigrationAnalysisDescriptor, string> ProcessRepositoryForManagedIdentityMigrationAndOpenPRAsync(
        Kernel kernel,
        [Description("Full GitHub repository URL. Can be inferred from app being CI/CD Enabled.Always confirm. (Eg. https://github.com/sanchitehta/sample-app)")] string repoUrl,
        [Description("Name of the branch to clone. Can be inferred from app's CI?CD Branch")] string branchToClone,
        [Description("Name of the branch to create with the fix.")] string branchName,
        [Description("SQLServer name in the original connection string. We are trying to migrate this to to use AD Based auth")] string sqlServer,
        [Description("Database in the original connection string")] string database)
    {
        return _codeAnalyzer.ProcessRepositoryForManagedIdentityMigrationAndOpenPRAsync(
            kernel,
            repoUrl: repoUrl,
            branchToClone: branchToClone,
            branchName: branchName,
            sqlServer: sqlServer,
            database: database);
    }

    [KernelFunction("get_status_process_repo_memory_leaks_open_pr")]
    [Description("To get execution status of process_repo_memory_leaks_open_pr function. Returns null if not started yet.")]
    public AsyncOperationStatusSummary<MemoryLeakeAnalysisDescriptor, string>? GetStatusAnalyzeAndFixMemoryLeaksAsync(
        [Description("Full GitHub repository URL. Can be inferred from app if CI/CD Enabled.Always confirm. (Eg. https://github.com/sanchitehta/sample-app)")] string repoUrl,
        [Description("Base branch name. Can be inferred from app if CI/CD Enabled.Always confirm")] string baseBranch,
        [Description("New branch name for fixes")] string newBranch)
    {
        return _codeAnalyzer.GetStatusAnalyzeAndFixMemoryLeaksAsync(
            repoUrl: repoUrl,
            baseBranch: baseBranch,
            newBranch: newBranch);
    }

    [KernelFunction("process_repo_memory_leaks_open_pr")]
    [Description(@"Analyze code for memory leaks in a code repository and create PR to user code with fixes.
User should be notified this operates on memory leak analysis and would try to find all bad practices of those objects.
<IMPORTANT>ONLY OFFER THIS CAPABILITY TO THE USER IF A MEMORY LEAK IS FOUND IN APP ON MONITORING, AND DO MENTION this is genral recommendation for all leaks based on object analysis</IMPORTANT> Ensure App remediation has been run, otherwise be relentless to offer a remidiation first")]
    public AsyncOperationStartResult<MemoryLeakeAnalysisDescriptor, string> AnalyzeAndFixMemoryLeaksAsync(
        Kernel kernel,
        [Description("Full GitHub repository URL. Can be inferred from app if CI/CD Enabled.Always confirm. (Eg. https://github.com/sanchitehta/sample-app)")] string repoUrl,
        [Description("Base branch name. Can be inferred from app if CI/CD Enabled.Always confirm")] string baseBranch,
        [Description("New branch name for fixes")] string newBranch,
        [Description("Description of memory analysis results and fixes that should be targeted in the repo scoped to the analysis")] string memoryAnalysis)
    {
        return _codeAnalyzer.AnalyzeAndFixMemoryLeaksAsync(
            kernel,
            repoUrl: repoUrl,
            baseBranch: baseBranch,
            newBranch: newBranch,
            memoryAnalysis: memoryAnalysis);
    }

    [KernelFunction("fetch_github_pull_requests")]
    [Description("Fetch github pull requests. If the returned object is empty and is not an exception, just let the user know there were none found. If there are more than 3 PRs matching, prompt the user to be more specific instead of returning all.")]
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
        return await _codeAnalyzer.FetchGithubIssues(
            repoUrl: repoUrl,
            issueFilter: issueFilter,
            itemStateFilter: itemStateFilter,
            milestone: milestone,
            assignee: assignee,
            creator: creator,
            mentioned: mentioned,
            labels: labels,
            since: since);
    }
}
