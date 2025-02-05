// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Octokit;
namespace Agent.Core.Plugins;

public class CodeAnalyzerService

{
    private readonly GitHubSettings _gitHubSettings;
    private readonly Octokit.GitHubClient _gitHubClient;
    private readonly AsyncOperationTracker<ManagedIdentityMigrationAnalysisDescriptor, string, string> _managedIdentityMigrationAnalysisTracker;
    private readonly AsyncOperationTracker<MemoryLeakeAnalysisDescriptor, string, string> _memoryLeakAnalysisTracker;
    private readonly ILogger<CodeAnalyzerService> _logger;

    public CodeAnalyzerService(IConfiguration configuration, GitHubClient gitHubClient, ILogger<CodeAnalyzerService> logger)
    {
        var azureSettings = configuration.GetSection("Azure").Get<AzureSettings>();
        _gitHubSettings = azureSettings.Github;
        _gitHubClient = gitHubClient;
        _managedIdentityMigrationAnalysisTracker = new(func: ProcessRepositoryForManagedIdentityMigrationAndOpenPRAsyncInternal);
        _memoryLeakAnalysisTracker = new(func: AnalyzeAndFixMemoryLeaksAsyncInternal);
        _logger = logger;
    }

    public AsyncOperationStatusSummary<ManagedIdentityMigrationAnalysisDescriptor, string>? GetProcessRepositoryForManagedIdentityMigrationAndOpenPRStatus
    (
        ManagedIdentityMigrationAnalysisDescriptor descriptor
    )
    {
        return _managedIdentityMigrationAnalysisTracker.GetOperationSummary(
            descriptor);
    }

    public AsyncOperationStartResult<ManagedIdentityMigrationAnalysisDescriptor, string> ProcessRepositoryForManagedIdentityMigrationAndOpenPRAsync(
        Kernel kernel,
        ManagedIdentityMigrationAnalysisDescriptor descriptor)
    {
        return _managedIdentityMigrationAnalysisTracker.TryStartOperation(
            kernel,
            contextMessage: $"Analyze GitHub repo for managed identity migration, repoUrl: {descriptor.repoUrl}",
            descriptor,
            parameter: "");
    }

    public AsyncOperationStatusSummary<MemoryLeakeAnalysisDescriptor, string>? GetStatusAnalyzeAndFixMemoryLeaksAsync(
        MemoryLeakeAnalysisDescriptor descriptor)
    {
        return _memoryLeakAnalysisTracker.GetOperationSummary(
            descriptor);
    }

    public AsyncOperationStartResult<MemoryLeakeAnalysisDescriptor, string> AnalyzeAndFixMemoryLeaksAsync(
        Kernel kernel,
        MemoryLeakeAnalysisDescriptor descriptor,
        string memoryAnalysis)
    {
        return _memoryLeakAnalysisTracker.TryStartOperation(
            kernel,
            contextMessage: $"Analyze GitHub to fix memory leak and open a new Github Issue. repoUrl: {descriptor.repoUrl}. Analysis Details: {memoryAnalysis}",
            descriptor,
            memoryAnalysis);
    }

    private async Task<List<(string path, string updatedContent)>> AnalyzeFilesForManagedIdentityAsync(
        Kernel kernel,
        List<(string path, string content)> codeFiles,
        string sqlServer,
        string database)
    {
        var fileUpdates = new List<(string path, string updatedContent)>();

        foreach (var (path, content) in codeFiles)
        {
            if (path.StartsWith(".github")) continue;

            var analysisPrompt = $@"Does this file need updates to use Azure Managed Identity for SQL auth? Reply only with 'true' or 'false'.
File: {path}
Content:
{content}";

            var needsUpdate = await kernel.InvokePromptAsync(analysisPrompt);

            if (needsUpdate?.ToString().Trim().Equals("true", StringComparison.OrdinalIgnoreCase) == true)
            {
                var updatePrompt = $@"Output only the complete updated code file content that implements Azure Managed Identity for SQL Server authentication. No markdown formatting, no code blocks.

Current file: {path}
Content:
{content}

Make these changes:
1. For .json/.config: Use connection string: Server={sqlServer};Database={database};Authentication=Active Directory Default;TrustServerCertificate=True;
2. For code files: Add Azure.Identity imports, use DefaultAzureCredential
3. Keep all non-SQL code unchanged
4. Keep exact formatting";

                var updatedContent = await kernel.InvokePromptAsync(updatePrompt);
                if (content != updatedContent?.ToString())
                {
                    fileUpdates.Add((path, updatedContent?.ToString() ?? content));
                }
            }
        }

        return fileUpdates;
    }

    private async Task<string> ProcessRepositoryForManagedIdentityMigrationAndOpenPRAsyncInternal(
        Kernel kernel,
        ManagedIdentityMigrationAnalysisDescriptor descriptor,
        string _parameter,
        Action<string> funcReportProgress,
        CancellationToken cancellationToken)
    {
        try
        {
            var token = _gitHubSettings.PatOverride;
            if (string.IsNullOrEmpty(_gitHubSettings.PatOverride))
            {
                return "No GH Token found";
            }

            var (owner, repo) = ParseGitHubUrl(descriptor.repoUrl);

            funcReportProgress("Fetching file structure from Github repo");

            // 1. Fetch all code files from the repository
            var codeFiles = await FetchCodeFilePathsAsync(owner, repo, descriptor.branchToClone);
            var filesWithContent = new List<(string path, string content)>();

            funcReportProgress("Fetching code from Github repo");

            // 2. Process each file and gather content
            foreach (var filePath in codeFiles)
            {
                var content = await FetchFileContentAsync(owner, repo, filePath, descriptor.branchToClone);
                filesWithContent.Add((filePath, content));
            }

            funcReportProgress("Analyzing code to find managed identity migration opportunity");

            var fileUpdates = await AnalyzeFilesForManagedIdentityAsync(kernel, filesWithContent, descriptor.sqlServer, descriptor.database);

            if (!fileUpdates.Any()) return "No changes needed for Managed Identity implementation.";

            funcReportProgress($"Creating pull request, modifying {fileUpdates.Count} files");

            var prUrl = await CreatePullRequestWithChangesAsync(
                kernel,
                owner,
                repo,
                descriptor.branchName,
                descriptor.branchToClone,
                fileUpdates.Select(u => (u.path, "", u.updatedContent)).ToList()
            );

            return $"Pull Request created: {prUrl}\nFiles modified: {fileUpdates.Count}";
        }
        catch (Exception ex)
        {
            throw new Exception($"Error processing repository: {ex.Message}", ex);
        }
    }

    private bool IsCodeFile(string path)
    {
        var codeExtensions = new HashSet<string>
        {
            ".cs", ".cshtml", ".csproj",  // .NET
            ".js", ".ts", ".jsx", ".tsx", // JavaScript/TypeScript
            ".py",                        // Python
            ".java",                      // Java
            ".go",                        // Go
            ".sql",                       // SQL
            ".yaml", ".yml",              // Configuration files
            ".xml", ".json"               // Configuration files
        };

        return codeExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
    }

    private async Task<List<string>> FetchCodeFilePathsAsync(string owner, string repo, string branch)
    {
        try
        {
            var reference = await _gitHubClient.Git.Reference.Get(owner, repo, $"heads/{branch}");
            var tree = await _gitHubClient.Git.Tree.GetRecursive(owner, repo, reference.Object.Sha);

            return tree.Tree
                .Where(item => item.Type == TreeType.Blob && IsCodeFile(item.Path))
                .Select(item => item.Path)
                .ToList();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error fetching code files: {ex.Message}", ex);
        }
    }

    private async Task<string> FetchFileContentAsync(string owner, string repo, string filePath, string branch)
    {
        try
        {
            var fileContent = await _gitHubClient.Repository.Content.GetRawContent(owner, repo, filePath);
            return Encoding.UTF8.GetString(fileContent);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error fetching file content for {filePath}: {ex.Message}", ex);
        }
    }

    private async Task<string> AnalyzeAndFixMemoryLeaksAsyncInternal(
        Kernel kernel,
        MemoryLeakeAnalysisDescriptor descriptor,
        string memoryAnalysis,
        Action<string> funcReportProgress,
        CancellationToken cancellationToken)
    {
        try
        {
            var (owner, repo) = ParseGitHubUrl(descriptor.repoUrl);

            funcReportProgress("Fetching file structure");

            var codeFiles = await FetchCodeFilePathsAsync(owner, repo, descriptor.baseBranch);
            var filesWithContent = new List<(string path, string content)>();

            funcReportProgress("Fetching code file content");

            foreach (var filePath in codeFiles)
            {
                var content = await FetchFileContentAsync(owner, repo, filePath, descriptor.baseBranch);
                filesWithContent.Add((filePath, content));
            }

            funcReportProgress("Analyzing code");

            var fileUpdates = await AnalyzeFilesForMemoryLeaksAsync(kernel, filesWithContent, memoryAnalysis);
            if (!fileUpdates.Any()) return "No memory leak fixes needed.";

            funcReportProgress("Creating pull request.");

            var prUrl = await CreatePullRequestWithChangesAsync(
                kernel,
                owner,
                repo,
                descriptor.newBranch,
                descriptor.baseBranch,
                memoryAnalysis,
                fileUpdates
            );

            return $"Pull Request created: {prUrl}";
        }
        catch (Exception ex)
        {
            throw new Exception($"Error analyzing repository: {ex.Message}", ex);
        }
    }

    private async Task<string> CreatePullRequestWithChangesAsync(
        Kernel kernel,
        string owner,
        string repo,
        string newBranch,
        string baseBranch,
        string analysisDecription,
        List<(string path, string updatedContent)> fileChanges)
    {
        var baseRef = await _gitHubClient.Git.Reference.Get(owner, repo, $"heads/{baseBranch}");
        var newRef = new NewReference($"refs/heads/{newBranch}", baseRef.Object.Sha);
        await _gitHubClient.Git.Reference.Create(owner, repo, newRef);

        foreach (var (path, updatedContent) in fileChanges)
        {
            try
            {
                var existingFile = await _gitHubClient.Repository.Content.GetAllContentsByRef(owner, repo, path, newBranch);
                var updateRequest = new UpdateFileRequest(
                    $"Fix memory leaks in {existingFile}",
                    updatedContent,
                    existingFile.First().Sha,
                    newBranch);

                await _gitHubClient.Repository.Content.UpdateFile(owner, repo, path, updateRequest);
            }
            catch (NotFoundException)
            {
                var createRequest = new CreateFileRequest(
                    "Add file with memory leak fixes",
                    updatedContent,
                    newBranch);

                await _gitHubClient.Repository.Content.CreateFile(owner, repo, path, createRequest);
            }
        }

        var prBody = new StringBuilder()
            .AppendLine("# Memory Leak Fixes\n")
            .AppendLine("This PR implements fixes for identified memory leaks:\n");

        foreach (var (path, _) in fileChanges)
        {
            prBody.AppendLine($"- Fixed memory leaks in: {path}");
        }


        var prDescriptionPrompt = $@"Create a PR description for fixing an app with identified memory leaks, analysis for fixes: {analysisDecription}. Current Description: {prBody}";

        var prDescriptionByLLM = await kernel.InvokePromptAsync(prDescriptionPrompt);

        var issue = await kernel.InvokePromptAsync(
                $"Create an issue for this pr:\n\n{prDescriptionByLLM}\n\nWith to this repo:\n\nhttps://github.com/sanchitmehta/sample-app. Return the schema of the created issue.",
                new(new PromptExecutionSettings()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Required
                    (
                        options: new() { AllowParallelCalls = true }
                    )
                })
            );
        _logger.LogInformation($"Issue created: {issue}");

        var newPr = new NewPullRequest(
            "Fix Memory Leaks",
            newBranch,
            baseBranch)
        {
            Body = prDescriptionByLLM.ToString()
        };

        var pr = await _gitHubClient.PullRequest.Create(owner, repo, newPr);

        var comment = await kernel.InvokePromptAsync(
                $"Link this pr to this issue by creating a comment on the PR:\n\nPR: {JsonSerializer.Serialize(pr)}\n\nIssue: {issue}",
                new(new PromptExecutionSettings()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Required
                    (
                        options: new() { AllowParallelCalls = true }
                    )
                })
            );
        _logger.LogInformation($"Comment added: {comment}");

        return pr.HtmlUrl;
    }

    private async Task<List<(string path, string updatedContent)>> AnalyzeFilesForMemoryLeaksAsync(
        Kernel kernel,
        List<(string path, string content)> codeFiles,
        string memoryAnalysis)
    {
        var fileUpdates = new List<(string path, string updatedContent)>();
        var memoryIssues = memoryAnalysis;

        foreach (var (path, content) in codeFiles)
        {
            if (path.Contains(".git") || path.Contains(".json")) continue;

            var updatePrompt = $@"Fix memory leaks in this code. We have already taken memory dumps and performed comparison on which objects are growing in heap memory size the most: {memoryAnalysis}

File: {path}
Content:
{content}

Requirements:
1. Suggest proper disposal patterns by adding comments, scope the fixes to only the findings but comment improvements
2. Fix resource leaks scoped to the findings
3. Ensure using statements are used for disposables
4. No markdown formatting, no code blocks.
5. Don't modify config json files
6. Think step by step

Output only the complete updated file content.";

            var updatedContent = await kernel.InvokePromptAsync(updatePrompt);
            if (content != updatedContent?.ToString())
            {
                fileUpdates.Add((path, updatedContent?.ToString() ?? content));
            }
        }

        return fileUpdates;
    }

    private (string owner, string repo) ParseGitHubUrl(string repoUrl)
    {
        var match = Regex.Match(repoUrl, @"github\.com[/:](?<owner>[\w.-]+)/(?<repo>[\w.-]+)(?:\.git)?$");
        if (!match.Success)
        {
            throw new ArgumentException("Invalid GitHub repository URL format");
        }

        return (match.Groups["owner"].Value, match.Groups["repo"].Value);
    }

    private async Task<string> CreatePullRequestWithChangesAsync(
         Kernel kernel,
         string owner,
         string repo,
         string newBranch,
         string baseBranch,
         List<(string path, string originalContent, string updatedContent)> fileChanges)
    {
        try
        {
            // 1. Get the reference to the base branch
            var baseRef = await _gitHubClient.Git.Reference.Get(owner, repo, $"heads/{baseBranch}");

            // 2. Create a new branch
            var newRef = new NewReference($"refs/heads/{newBranch}", baseRef.Object.Sha);
            await _gitHubClient.Git.Reference.Create(owner, repo, newRef);

            // 3. Create or update each modified file
            foreach (var (path, _, updatedContent) in fileChanges)
            {

                try
                {
                    var existingFile = await _gitHubClient.Repository.Content.GetAllContentsByRef(owner, repo, path, newBranch);
                    var updateFileRequest = new UpdateFileRequest(
                        "Update for Managed Identity implementation",
                        updatedContent,
                        existingFile.First().Sha,
                        newBranch);

                    await _gitHubClient.Repository.Content.UpdateFile(
                        owner,
                        repo,
                        path,
                        updateFileRequest);
                }
                catch (NotFoundException)
                {
                    // Create new file if it doesn't exist
                    var createFileRequest = new CreateFileRequest(
                        "Add file with Managed Identity implementation",
                        updatedContent,
                        newBranch);

                    await _gitHubClient.Repository.Content.CreateFile(
                        owner,
                        repo,
                        path,
                        createFileRequest);
                }
            }

            // 4. Create the pull request
            var prBody = new StringBuilder();
            prBody.AppendLine("# Managed Identity Implementation Changes\n");
            prBody.AppendLine("This PR implements Azure Managed Identity for SQL authentication with the following changes:\n");

            foreach (var (path, _, _) in fileChanges)
            {
                prBody.AppendLine($"- Modified: {path}");
            }

            var issue = await kernel.InvokePromptAsync(
                $"Create an issue for this pr:\n\n{prBody}\n\nWith to this repo:\n\nhttps://github.com/sanchitmehta/sample-app. Return the schema of the created issue.",
                new(new PromptExecutionSettings()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Required
                    (
                        options: new() { AllowParallelCalls = true }
                    )
                })
            );
            _logger.LogInformation($"Issue created: {issue}");

            var newPr = new NewPullRequest(
                "Implement SQL Managed Identity Authentication",
                newBranch,
                baseBranch)
            {
                Body = prBody.ToString()
            };

            var pr = await _gitHubClient.PullRequest.Create(owner, repo, newPr);

            var comment = await kernel.InvokePromptAsync(
                $"Link this pr to this issue by creating a comment on the PR:\n\nPR: {JsonSerializer.Serialize(pr)}\n\nIssue: {issue}",
                new(new PromptExecutionSettings()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Required
                    (
                        options: new() { AllowParallelCalls = true }
                    )
                })
            );
            _logger.LogInformation($"Comment added: {comment}");

            return pr.HtmlUrl;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error creating pull request: {ex.Message}", ex);
        }
    }
}

public sealed record ManagedIdentityMigrationAnalysisDescriptor(
    [Description("Full GitHub repository URL. Can be inferred from app being CI/CD Enabled.Always confirm")] string repoUrl,
    [Description("Name of the branch to clone. Can be inferred from app's CI?CD Branch")] string branchToClone,
    [Description("Name of the branch to create with the fix.")] string branchName,
    [Description("SQLServer name in the original connection string. We are trying to migrate this to to use AD Based auth")] string sqlServer,
    [Description("Database in the original connection string")] string database);

public sealed record MemoryLeakeAnalysisDescriptor(
    [Description("Full GitHub repository URL. Can be inferred from app if CI/CD Enabled.Always confirm")] string repoUrl,
    [Description("Base branch name. Can be inferred from app if CI/CD Enabled.Always confirm")] string baseBranch,
    [Description("New branch name for fixes")] string newBranch);
