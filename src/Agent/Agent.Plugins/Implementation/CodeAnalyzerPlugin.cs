using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Plugins.CodeAnalyzer;
using Agent.Plugins.Helpers;
using Agent.Plugins.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Octokit;

namespace Agent.Plugins.Implementation
{
    public class CodeAnalyzerPlugin : ICodeAnalyzerPlugin
    {
        private const int MaxChunkSize = 2000;
        private readonly GitHubSettings _gitHubSettings;
        private readonly TeamsConnector _teamsConnector;
        private readonly CodeAnalyzerService _codeAnalyzer;
        private readonly Octokit.GitHubClient _gitHubClient;

        private readonly ILogger<CodeAnalyzerPlugin> _logger;

        public CodeAnalyzerPlugin(
            CodeAnalyzerService codeAnalyzerService,
            GitHubSettings gitHubSettings,
            TeamsConnector teamsConnector,
            Models.GitHubClient gitHubClient,
            ILogger<CodeAnalyzerPlugin> logger)
        {
            _logger = logger;
            _gitHubSettings = gitHubSettings;
            _teamsConnector = teamsConnector;
            _codeAnalyzer = codeAnalyzerService;
            _gitHubClient = gitHubClient.Client;
        }

        public AsyncOperationStartResult<MemoryLeakeAnalysisDescriptor, string> AnalyzeAndFixMemoryLeaksAsync(
            Kernel kernel, 
            string repoUrl, 
            string baseBranch, 
            string newBranch,
            string memoryAnalysis)
        {
            if (repoUrl.EndsWith(".git"))
            {
                repoUrl = repoUrl.Replace(".git", "");
            }
            var descriptor = new MemoryLeakeAnalysisDescriptor(repoUrl, baseBranch, newBranch);
            return _codeAnalyzer.AnalyzeAndFixMemoryLeaksAsync(
                kernel,
                descriptor,
                memoryAnalysis);
        }

        public async Task<IEnumerable<GithubIssuePluginIssue>> FetchGithubIssues(
            string repoUrl, 
            GithubIssuePluginIssueFilter issueFilter, 
            GithubIssuePluginItemStateFilter itemStateFilter,
            string milestone,
            string assignee, 
            string? creator, 
            string? mentioned, 
            string[]? labels, 
            DateTimeOffset? since)
        {
            return await KernelFunctionHelpers.TryAction(
                nameof(CodeAnalyzerPlugin),
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
                    return res.Where(issue => issue.PullRequest != null).Select(issue => issue.ToGithubIssuePluginIssue());
                },
                _logger
            );
        }

        public AsyncOperationStatusSummary<ManagedIdentityMigrationAnalysisDescriptor, string>? GetProcessRepositoryForManagedIdentityMigrationAndOpenPRStatus(
            Kernel kernel,
            string repoUrl,
            string branchToClone,
            string branchName, 
            string sqlServer, 
            string database)
        {
            if (repoUrl.EndsWith(".git"))
            {
                repoUrl = repoUrl.Replace(".git", "");
            }
            var descriptor = new ManagedIdentityMigrationAnalysisDescriptor(
                repoUrl,
                branchToClone,
                branchName,
                sqlServer,
                database);
            return _codeAnalyzer.GetProcessRepositoryForManagedIdentityMigrationAndOpenPRStatus(
                descriptor);
        }

        public AsyncOperationStatusSummary<MemoryLeakeAnalysisDescriptor, string>? GetStatusAnalyzeAndFixMemoryLeaksAsync(
            string repoUrl, 
            string baseBranch, 
            string newBranch)
        {
            if (repoUrl.EndsWith(".git"))
            {
                repoUrl = repoUrl.Replace(".git", "");
            }

            var descriptor = new MemoryLeakeAnalysisDescriptor(
                repoUrl,
                baseBranch,
                newBranch);
            return _codeAnalyzer.GetStatusAnalyzeAndFixMemoryLeaksAsync(
                descriptor);
        }

        public AsyncOperationStartResult<ManagedIdentityMigrationAnalysisDescriptor, string> ProcessRepositoryForManagedIdentityMigrationAndOpenPRAsync(
            Kernel kernel, 
            string repoUrl,
            string branchToClone, 
            string branchName, 
            string sqlServer, 
            string database)
        {
            if (repoUrl.EndsWith(".git"))
            {
                repoUrl = repoUrl.Replace(".git", "");
            }
            var descriptor = new ManagedIdentityMigrationAnalysisDescriptor(
                repoUrl,
                branchToClone,
                branchName,
                sqlServer,
                database);
            return _codeAnalyzer.ProcessRepositoryForManagedIdentityMigrationAndOpenPRAsync(
                kernel,
                descriptor);
        }

        public async Task<string> RequestGitHubTokenAsync(
            string userMessage,
            string repository)
        {
            var state = Guid.NewGuid().ToString();
            var loginUrl = $"https://mikarmar-githubauth-app-h2cmg7b7cybkhees.westus2-01.azurewebsites.net/.auth/me";

            if (await GitHubTokenManager.TokenExistsAsync())
            {
                return "Github logged in already has been retried already";
            }

            var message = $@"# ?? GitHub Authorization Required

                - {userMessage}

                - This Action would get permission scan your Github Repo

                ?? **[Authorize GitHub Access]({loginUrl})**"; ;
            await _teamsConnector.PostMessageAsync(new TeamsMessage(message));
            return "GitHub authorization request sent to Teams, wait for callback";
        }
    }
}
