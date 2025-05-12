using System.ComponentModel;
using System.Threading.Tasks;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;

namespace FirstPartyAgent.Core.Plugins
{
    public class AzureDevOpsPlugin
    {
        private readonly IAzureDevOpsClient _client;
        private readonly ILogger<AzureDevOpsPlugin> _logger;
        private readonly ITeamsClient _teamsClient;
        private readonly ISessionMessageService _sessionMessageService;

        [Description("Azure DevOps Plugin to carry out operations related to Azure Devops repositories. All links returned to the user should be user-friendly and not API links.")]
        public AzureDevOpsPlugin(IAzureDevOpsClient client, ILogger<AzureDevOpsPlugin> logger, ITeamsClient teamsClient, ISessionMessageService sessionMessageService)
        {
            _client = client;
            _logger = logger;
            _teamsClient = teamsClient;
            _sessionMessageService = sessionMessageService;
        }

        [KernelFunction("list_files_in_repo_path")]
        [Description("Lists all files in a repo path, upto a max of topN files")]
        public async Task<string> ListFilesAsync(string pathInRepo, int topN, Kernel kernel)
        {
            var logMessage = $"[list_files_in_repo_path][{DateTime.UtcNow}] Invoked with path: {pathInRepo}.";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            return await _client.ListFilesAsync(pathInRepo, topN);
        }

        [KernelFunction("read_file_at_path")]
        [Description("Reads a file at a given path")]
        public async Task<string> ReadFileAsync(string filePath, string branch, Kernel kernel)
        {
            var logMessage = $"[read_file_at_path][{DateTime.UtcNow}] Invoked with file path: {filePath}.";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            return await _client.ReadFileAsync(filePath, branch);
        }

        [KernelFunction("get_commit_history")]
        [Description("Gets the commit history of the repository upto topN commits")]
        public async Task<string> GetCommitHistoryAsync(int topN, Kernel kernel)
        {
            var logMessage = $"[get_commit_history][{DateTime.UtcNow}] Invoked with topN: {topN}.";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            return await _client.GetCommitHistoryAsync(topN);
        }

        [KernelFunction("create_new_branch")]
        [Description("Creates a new branch in the repository from the main branch")]
        public async Task<string> CreateBranchAsync(string newBranchName, Kernel kernel)
        {
            var logMessage = $"[create_new_branch][{DateTime.UtcNow}] Invoked with new branch name: {newBranchName}.";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            return await _client.CreateBranchAsync(_client.MainBranchName, newBranchName);
        }

        [KernelFunction("create_commit")]
        [Description("Creates a commit in the repository give branchName, filePath, fileContent, commitMessage")]
        public async Task<string> CreateCommitAsync(string branchName, string filePath, string fileContent, string commitMessage, Kernel kernel)
        {
            var logMessage = $"[create_commit][{DateTime.UtcNow}] Invoked with branch name: {branchName}, file path: {filePath}, commit message: {commitMessage}.";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            return await _client.CreateCommitAsync(branchName, filePath, fileContent, commitMessage);
        }

        [KernelFunction("create_pull_request")]
        [Description("Creates a pull request from a source branch to a target branch with a title and optional description")]
        public async Task<string> CreatePullRequestAsync(string sourceBranchName, string targetBranchName, string title, string description, Kernel kernel)
        {
            var logMessage = $"[create_pull_request][{DateTime.UtcNow}] Invoked with source: {sourceBranchName}, target: {targetBranchName}, title: {title}.";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            return await _client.CreatePullRequestAsync(sourceBranchName, targetBranchName, title, description);
        }

        [KernelFunction("abandon_pull_request")]
        [Description("Abandons (closes) a pull request given its pull request ID")]
        public async Task<string> AbandonPullRequestAsync(int pullRequestId, Kernel kernel)
        {
            var logMessage = $"[abandon_pull_request][{DateTime.UtcNow}] Invoked with pull request ID: {pullRequestId}.";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            return await _client.AbandonPullRequestAsync(pullRequestId);
        }

        [KernelFunction("search_code")]
        [Description("Searches code in the repository using a search string and returns up to topN results")]
        public async Task<string> SearchCodeAsync(string searchText, int topN, Kernel kernel)
        {
            var logMessage = $"[search_code][{DateTime.UtcNow}] Invoked with search text: {searchText}, topN: {topN}.";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            return await _client.SearchCodeAsync(searchText, topN);
        }
    }
}
