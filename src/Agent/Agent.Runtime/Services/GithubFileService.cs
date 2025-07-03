using Octokit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Agent.Plugins.Models;
using System.Net.Http.Headers;
using Agent.Core.Configuration;

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
            var (owner, repo, branch, path) = ParseGitHubRepoPath(repoPath);

            // If no path is specified, list root
            var contents = string.IsNullOrEmpty(path)
                ? await _gitHubClient.Repository.Content.GetAllContents(owner, repo, branch)
                : await _gitHubClient.Repository.Content.GetAllContentsByRef(owner, repo, path, branch);

            return contents;
        }

        /// <summary>
        /// Downloads all .yaml/.yml files in a given repository path and saves them to the specified local folder.
        /// </summary>
        /// <param name="repoPath">GitHub repo path (see ListFilesInRepoPath)</param>
        /// <param name="folderPath">Local folder to save files to</param>
        /// <returns>Dictionary of file path (relative to repo root) to local file path</returns>
        public async Task<Dictionary<string, string>> DownloadYamlFilesInRepoPath(string repoPath, string folderPath)
        {
            var yamlFiles = new Dictionary<string, string>();
            var files = await ListFilesInRepoPath(repoPath);

            Directory.CreateDirectory(folderPath);

            foreach (var file in files.Where(f =>
                f.Type == ContentType.File &&
                (f.Name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || f.Name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))))
            {
                if (!string.IsNullOrEmpty(file.DownloadUrl))
                {
                    var content = await _httpClient.GetStringAsync(file.DownloadUrl);

                    // Save to local file, preserving directory structure
                    var localFilePath = Path.Combine(folderPath, file.Path.Replace('/', Path.DirectorySeparatorChar));
                    var localDir = Path.GetDirectoryName(localFilePath);
                    if (!string.IsNullOrEmpty(localDir))
                        Directory.CreateDirectory(localDir);

                    await File.WriteAllTextAsync(localFilePath, content);
                    yamlFiles[file.Path] = localFilePath;
                }
            }

            return yamlFiles;
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

        public Task<Dictionary<string, string>> DownloadYamlFilesInRepoPath(string repoPath, string folderPath)
        {
            var emptyDict = new Dictionary<string, string>();
            return Task.FromResult(emptyDict);
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
        Task<Dictionary<string, string>> DownloadYamlFilesInRepoPath(string repoPath, string folderPath);
    }
}
