using System.ComponentModel;
using Agents.Core.Configuration;
using Agents.Core.Helpers;
using Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Octokit;


namespace Agents.Core.Plugins;

public class CodeAnalyzerPlugin
{
    private const int MaxChunkSize = 2000;
    private readonly GitHubSettings _gitHubSettings;
    private readonly TeamsConnector _teamsConnector;
    private readonly CodeAnalyzerService _codeAnalyzer;
    private readonly Octokit.GitHubClient _gitHubClient;

    private readonly ILogger<CodeAnalyzerPlugin> _logger;


    public CodeAnalyzerPlugin(
        CodeAnalyzerService codeAnalyzerService,
        IConfiguration configuration,
        TeamsConnector teamsConnector,
        Models.GitHubClient gitHubClient,
        ILogger<CodeAnalyzerPlugin> logger)
    {
        var azureSettings = configuration.GetSection("Azure").Get<AzureSettings>();
        _logger = logger;
        _gitHubSettings = azureSettings.Github;
        _teamsConnector = teamsConnector;
        _codeAnalyzer = codeAnalyzerService;
        _gitHubClient = gitHubClient.Client;
    }

    [KernelFunction("request_github_token_for_gh_actions")]
    [Description("Request GitHub token from user via Teams")]
    public async Task<string> RequestGitHubTokenAsync(
        [Description("Message to user posted regarding why Github login is needed")] string userMessage,
        [Description("Github full repository")] string repository)
    {
        var state = Guid.NewGuid().ToString();
        var loginUrl = $"https://github.com/login/oauth/authorize" +
            $"?client_id={_gitHubSettings.ClientId}" +
            $"&redirect_uri={Uri.EscapeDataString(_gitHubSettings.CallbackUrl)}" +
            $"&scope=repo" +
            $"&state={state}";

        if (await GitHubTokenManager.TokenExistsAsync())
        {
            return "Github logged in already has been retried already";
        }

        var message = $@"# 🔐 GitHub Authorization Required

- {userMessage}

- This Action would get permission scan your Github Repo

▶️ **[Authorize GitHub Access]({loginUrl})**"; ;
        await _teamsConnector.PostMessageAsync(new TeamsMessage(message));
        return "GitHub authorization request sent to Teams, wait for callback";
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

    [KernelFunction("get_status_process_repo_memory_leaks_open_pr")]
    [Description("To get execution status of process_repo_memory_leaks_open_pr function. Returns null if not started yet.")]
    public AsyncOperationStatusSummary<MemoryLeakeAnalysisDescriptor, string>? GetStatusAnalyzeAndFixMemoryLeaksAsync(
        [Description("Full GitHub repository URL. Can be inferred from app if CI/CD Enabled.Always confirm. (Eg. https://github.com/sanchitehta/sample-app)")] string repoUrl,
        [Description("Base branch name. Can be inferred from app if CI/CD Enabled.Always confirm")] string baseBranch,
        [Description("New branch name for fixes")] string newBranch)
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
}
