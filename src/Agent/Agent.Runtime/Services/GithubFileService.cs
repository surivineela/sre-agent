using System.Net.Http.Headers;
using Agent.Core.Configuration;
using Agent.Framework;
using Octokit;

namespace Agent.Runtime.Services
{
    public class GithubFileService : IGithubFileService
    {
        private readonly Octokit.GitHubClient _gitHubClient;
        private readonly GitHubSettings _gitHubSettings;

        // Static HttpClient shared by all instances
        private static readonly HttpClient _httpClient = new HttpClient();

        public GithubFileService(Agent.Plugins.Models.GitHubClient gitHubClient, GitHubSettings gitHubSettings)
        {
            _gitHubClient = gitHubClient.Client;
            _gitHubSettings = gitHubSettings;
            InitializeHttpClient();
        }

        /// <summary>
        /// Lists files in a given repository path.
        /// </summary>
        /// <param name="repoPath">Format: https://github.com/{owner}/{repo}/tree/{branch}/{path} or https://github.com/{owner}/{repo}</param>
        /// <returns>List of file names (with paths relative to the repo root)</returns>
        public async Task<IReadOnlyList<RepositoryContent>> ListFilesInRepoPath(string repoPath)
        {
            try
            {
                var (owner, repo, branch, path) = ParseGitHubRepoPath(repoPath);

                var contents = string.IsNullOrEmpty(path)
                    ? await _gitHubClient.Repository.Content.GetAllContentsByRef(owner, repo, branch)
                    : await _gitHubClient.Repository.Content.GetAllContentsByRef(owner, repo, path, branch);

                return contents;
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                throw new InvalidOperationException($"Failed to list files in repository path '{repoPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Downloads all .yaml/.yml files in a given repository path and saves them to the specified local folder.
        /// </summary>
        /// <param name="repoPath">GitHub repo path (see ListFilesInRepoPath)</param>
        /// <param name="folderPath">Local folder to save files to</param>
        /// <returns>Dictionary of file path (relative to repo root) to local file path</returns>
        public async Task<CustomAgentFiles> DownloadYamlFilesInRepoPath(string repoPath, string folderPath)
        {
            var agentFiles = new CustomAgentFiles(
                yaml: new Dictionary<string, string>(),
                kql: new Dictionary<string, string>(),
                appsettings: new Dictionary<string, string>()
            );

            // Ensure clean folder state
            if (Directory.Exists(folderPath))
                Directory.Delete(folderPath, recursive: true);

            Directory.CreateDirectory(folderPath);

            await DownloadFilesRecursive(repoPath, folderPath, agentFiles);

            return agentFiles;
        }

        private async Task DownloadFilesRecursive(string repoPath, string folderPath, CustomAgentFiles agentFiles)
        {
            var files = await ListFilesInRepoPath(repoPath);

            foreach (var file in files)
            {
                if (file.Type == ContentType.Dir)
                {
                    // Parse the repo path to get owner, repo, branch, and current path
                    var (owner, repo, branch, currentPath) = ParseGitHubRepoPath(repoPath);
                    // Build the new path for the subdirectory
                    var newPath = string.IsNullOrEmpty(currentPath) ? file.Name : $"{currentPath}/{file.Name}";
                    // Reconstruct the repo path for the subdirectory
                    var subRepoPath = $"https://github.com/{owner}/{repo}/tree/{branch}/{newPath}";
                    await DownloadFilesRecursive(subRepoPath, folderPath, agentFiles);
                }
                else if (file.Type == ContentType.File && !string.IsNullOrEmpty(file.DownloadUrl))
                {
                    var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                    if (ext == ".yaml" || ext == ".yml" || ext == ".kql" || ext == ".json")
                    {
                        var content = await _httpClient.GetStringAsync(file.DownloadUrl);

                        var localFilePath = Path.Combine(folderPath, file.Path.Replace('/', Path.DirectorySeparatorChar));
                        var localDir = Path.GetDirectoryName(localFilePath);
                        if (!string.IsNullOrEmpty(localDir))
                            Directory.CreateDirectory(localDir);

                        await File.WriteAllTextAsync(localFilePath, content);

                        // Update the appropriate dictionary
                        if (ext == ".yaml" || ext == ".yml")
                            agentFiles.yaml[file.Path] = localFilePath;
                        else if (ext == ".kql")
                            agentFiles.kql[file.Path] = localFilePath;
                        else if (ext == ".json")
                            agentFiles.appsettings[file.Path] = localFilePath;
                    }
                }
            }
        }

        /// <summary>
        /// Parses a GitHub repo path URL into owner, repo, branch, and path.
        /// </summary>
        private static (string owner, string repo, string branch, string path) ParseGitHubRepoPath(string repoPath)
        {
            // Example: https://github.com/owner/repo/tree/branch/path/to/dir
            var uri = new Uri(repoPath);
            var segments = uri.AbsolutePath.Trim('/').Split('/');

            if (segments.Length < 2)
                throw new ArgumentException("Invalid GitHub repo path. Must be at least https://github.com/{owner}/{repo}");

            string owner = segments[0];
            string repo = segments[1];
            string branch = "main";
            string path = "";

            if (segments.Length > 2 && segments[2] == "tree")
            {
                if (segments.Length < 4)
                    throw new ArgumentException("Invalid GitHub repo path. Missing branch name after /tree/");

                branch = segments[3];
                if (segments.Length > 4)
                    path = string.Join("/", segments[4..]);
            }

            return (owner, repo, branch, path);
        }

        private void InitializeHttpClient()
        {
            var gitHubAccessToken = _gitHubSettings.PatTokenOverride;
            if (!string.IsNullOrEmpty(gitHubAccessToken) && gitHubAccessToken != "replace")
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", gitHubAccessToken);
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }
        }
    }
    /// <summary>
    /// A null-object implementation of IGithubFileService that returns empty results.
    /// </summary>
    public class NullableGithubFileService : IGithubFileService
    {
        public Task<IReadOnlyList<RepositoryContent>> ListFilesInRepoPath(string repoPath)
        {
            IReadOnlyList<RepositoryContent> emptyList = Array.Empty<RepositoryContent>();
            return Task.FromResult(emptyList);
        }

        public Task<CustomAgentFiles> DownloadYamlFilesInRepoPath(string repoPath, string folderPath)
        {
            return Task.FromResult(
                new CustomAgentFiles(
                    new Dictionary<string, string>(),
                    new Dictionary<string, string>(),
                    new Dictionary<string, string>())
                );
        }
    }
    public interface IGithubFileService
    {
        /// <summary>
        /// Lists files in a given repository path.
        /// </summary>
        /// <param name="repoPath">Format: https://github.com/{owner}/{repo}/tree/{branch}/{path} or https://github.com/{owner}/{repo}</param>
        /// <returns>List of file names (with paths relative to the repo root)</returns>
        Task<IReadOnlyList<RepositoryContent>> ListFilesInRepoPath(string repoPath);

        /// <summary>
        /// Downloads all .yaml/.yml files in a given repository path and saves them to the specified local folder.
        /// </summary>
        /// <param name="repoPath">GitHub repo path (see ListFilesInRepoPath)</param>
        /// <param name="folderPath">Local folder to save files to</param>
        /// <returns>Dictionary of file path (relative to repo root) to local file path</returns>
        Task<CustomAgentFiles> DownloadYamlFilesInRepoPath(string repoPath, string folderPath);
    }
}
