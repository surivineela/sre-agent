// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

/// <summary>
/// Command handlers for workspace-related operations.
/// </summary>
public static class WorkspaceCommandHandlers
{
    // Memory folder constants (same as in CommandBuilder.Workspace.cs and AgentFileStorageService)
    private const string RepoInstructionsFolderName = ".github";
    private const string SessionInsightsFolderName = "sessionInsights";
    private const string SynthesizedKnowledgeFolderName = "synthesizedKnowledge";

    #region Repo Instructions Handlers

    /// <summary>
    /// Handles the repo-instructions upload command.
    /// </summary>
    public static async Task<int> HandleRepoInstructionsUploadCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting repo-instructions upload command");

        var specifiedPath = parseResult.GetValue(WorkspaceCommandOptions.Memory.PathOption);
        var repoName = parseResult.GetValue(WorkspaceCommandOptions.Memory.RepoOption);

        // Determine source path
        // If --path is provided, use it directly without appending subfolders
        // Otherwise, use default path with subfolders: {repoName}/.github
        string sourcePath;
        if (!string.IsNullOrWhiteSpace(specifiedPath))
        {
            sourcePath = specifiedPath;
        }
        else
        {
            var localPath = WorkspaceCommandOptions.Memory.GetEffectivePath(null);
            if (!string.IsNullOrWhiteSpace(repoName))
            {
                sourcePath = Path.Combine(localPath, repoName, RepoInstructionsFolderName);
            }
            else
            {
                // For backwards compatibility, when no repo is specified, look in root .github
                sourcePath = Path.Combine(localPath, RepoInstructionsFolderName);
            }
        }

        DebugLogger.Debug("Parameters", $"Path: {sourcePath}, RepoName: {repoName ?? "all"}");

        // Validate path exists
        if (!Directory.Exists(sourcePath))
        {
            ConsoleUI.WriteStatus(false, $"Repo instructions directory not found: {sourcePath}");
            ConsoleUI.WriteInfo($"Expected path: {sourcePath}", ConsoleColor.Yellow);
            return 1;
        }

        // Count files
        var fileCount = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories).Length;
        if (fileCount == 0)
        {
            ConsoleUI.WriteStatus(false, "No files found in repo instructions directory.");
            return 1;
        }

        // Show what will be uploaded
        var repoDesc = repoName ?? "all repos";
        ConsoleUI.WriteInfo($"Uploading repo instructions for '{repoDesc}' from: {sourcePath}", ConsoleColor.Cyan);
        ConsoleUI.WriteBullet($"{fileCount} file(s) to upload", ConsoleColor.Gray);
        Console.WriteLine();

        // Create tar.gz archive
        ConsoleUI.WriteInfo("Creating archive...", ConsoleColor.Gray);
        byte[] tarGzData;
        try
        {
            tarGzData = await TarGzHelper.CreateTarGzFromFoldersAsync(sourcePath, ["."], cancellationToken);
            DebugLogger.Debug("Archive", $"Created tar.gz archive: {tarGzData.Length:N0} bytes");
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Failed to create archive: {ex.Message}");
            return 1;
        }

        // Upload to server
        ConsoleUI.WriteInfo($"Uploading ({tarGzData.Length:N0} bytes)...", ConsoleColor.Gray);
        using var apiService = new ApiService();
        var (success, response) = await apiService.UploadRepoInstructionsAsync(tarGzData, repoName);

        if (success)
        {
            Console.WriteLine();
            ConsoleUI.WriteStatus(true, $"Repo instructions for '{repoDesc}' uploaded successfully.");
            ConsoleUI.WriteInfo(response, ConsoleColor.Gray);
            return 0;
        }
        else
        {
            Console.WriteLine();
            ConsoleUI.WriteStatus(false, response);
            return 1;
        }
    }

    /// <summary>
    /// Handles the repo-instructions download command.
    /// </summary>
    public static async Task<int> HandleRepoInstructionsDownloadCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting repo-instructions download command");

        var specifiedPath = parseResult.GetValue(WorkspaceCommandOptions.Memory.PathOption);
        var repoName = parseResult.GetValue(WorkspaceCommandOptions.Memory.RepoOption);

        using var apiService = new ApiService();

        // Determine destination path
        // If --path is provided, use it directly without appending subfolders
        // Otherwise, use default path with subfolders: {repoName}/.github
        string destPath;
        if (!string.IsNullOrWhiteSpace(specifiedPath))
        {
            destPath = specifiedPath;
        }
        else
        {
            var localPath = WorkspaceCommandOptions.Memory.GetEffectivePath(null);
            if (!string.IsNullOrWhiteSpace(repoName))
            {
                destPath = Path.Combine(localPath, repoName, RepoInstructionsFolderName);
            }
            else
            {
                // For backwards compatibility, when no repo is specified, use root .github
                destPath = Path.Combine(localPath, RepoInstructionsFolderName);
            }
        }

        DebugLogger.Debug("Parameters", $"Path: {destPath}, RepoName: {repoName ?? "all"}");

        // Show what will be downloaded
        var repoDesc = repoName ?? "all repos";
        ConsoleUI.WriteInfo($"Downloading repo instructions for '{repoDesc}' to: {destPath}", ConsoleColor.Cyan);
        Console.WriteLine();

        // Download from server
        ConsoleUI.WriteInfo("Downloading...", ConsoleColor.Gray);
        var (success, tarGzData, errorMessage) = await apiService.DownloadRepoInstructionsAsync(repoName);

        if (!success || tarGzData.Length == 0)
        {
            ConsoleUI.WriteStatus(false, errorMessage);
            return 1;
        }

        DebugLogger.Debug("Download", $"Received tar.gz archive: {tarGzData.Length:N0} bytes");
        ConsoleUI.WriteInfo($"Received {tarGzData.Length:N0} bytes", ConsoleColor.Gray);

        // Extract to local path
        ConsoleUI.WriteInfo("Extracting...", ConsoleColor.Gray);
        try
        {
            Directory.CreateDirectory(destPath);
            await TarGzHelper.ExtractTarGzAsync(tarGzData, destPath, cancellationToken);

            var extractedFiles = Directory.GetFiles(destPath, "*", SearchOption.AllDirectories).Length;

            Console.WriteLine();
            ConsoleUI.WriteStatus(true, $"Repo instructions for '{repoDesc}' downloaded successfully ({extractedFiles} file(s)).");
            ConsoleUI.WriteKeyValue("Location", destPath, 0);
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Failed to extract archive: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the repo-instructions delete command.
    /// </summary>
    public static async Task<int> HandleRepoInstructionsDeleteCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting repo-instructions delete command");

        var repoName = parseResult.GetValue(WorkspaceCommandOptions.Memory.RepoOption);

        using var apiService = new ApiService();

        DebugLogger.Debug("Parameters", $"RepoName: {repoName ?? "all"}");

        // Confirm deletion
        var repoDesc = repoName ?? "all repos";
        ConsoleUI.WriteInfo($"Deleting repo instructions for '{repoDesc}' from remote agent...", ConsoleColor.Cyan);
        Console.WriteLine();

        // Delete from server
        ConsoleUI.WriteInfo("Deleting...", ConsoleColor.Gray);
        var (success, response) = await apiService.DeleteRepoInstructionsAsync(repoName);

        if (success)
        {
            Console.WriteLine();
            ConsoleUI.WriteStatus(true, $"Repo instructions for '{repoDesc}' deleted successfully.");
            ConsoleUI.WriteInfo(response, ConsoleColor.Gray);
            return 0;
        }
        else
        {
            Console.WriteLine();
            ConsoleUI.WriteStatus(false, response);
            return 1;
        }
    }

    #endregion

    #region Sync Handler

    /// <summary>
    /// Handles the workspace sync command. Downloads all workspace memory files.
    /// </summary>
    public static async Task<int> HandleSyncCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting workspace sync command");

        var specifiedPath = parseResult.GetValue(WorkspaceCommandOptions.Memory.PathOption);
        var includeSessionInsights = parseResult.GetValue(WorkspaceCommandOptions.Memory.IncludeSessionInsightsOption);
        var threadIdStr = parseResult.GetValue(WorkspaceCommandOptions.Memory.ThreadIdOption);

        Guid? threadId;
        try
        {
            threadId = WorkspaceCommandOptions.Memory.ParseThreadId(threadIdStr);
        }
        catch (ArgumentException ex)
        {
            ConsoleUI.WriteStatus(false, ex.Message);
            return 1;
        }

        var basePath = WorkspaceCommandOptions.Memory.GetEffectivePath(specifiedPath);
        DebugLogger.Debug("Parameters", $"Path: {basePath}, IncludeSessionInsights: {includeSessionInsights}, ThreadId: {threadId?.ToString() ?? "none"}");

        ConsoleUI.WriteInfo($"Syncing workspace memory to: {basePath}", ConsoleColor.Cyan);
        Console.WriteLine();

        using var apiService = new ApiService();
        var totalFiles = 0;
        var hasErrors = false;

        // 1. Download repo instructions for all repos
        ConsoleUI.WriteInfo("Downloading repo instructions...", ConsoleColor.Gray);
        var (repoSuccess, repoList, repoErrorMessage) = await apiService.ListWorkspaceMemoryAsync("repo-instructions");
        if (repoSuccess && repoList != null)
        {
            // Get unique repo names from the file paths (path format: {repoName}/.github/{file})
            var repoNames = repoList.Files
                .Select(f => f.Path.Split('/').FirstOrDefault())
                .Where(r => !string.IsNullOrEmpty(r))
                .Distinct()
                .ToList();

            foreach (var repoName in repoNames)
            {
                if (string.IsNullOrEmpty(repoName)) continue;

                var (success, tarGzData, errorMessage) = await apiService.DownloadRepoInstructionsAsync(repoName);
                if (success && tarGzData.Length > 0)
                {
                    var destPath = string.IsNullOrWhiteSpace(specifiedPath)
                        ? Path.Combine(basePath, repoName!, RepoInstructionsFolderName)
                        : Path.Combine(basePath, repoName!, RepoInstructionsFolderName);

                    try
                    {
                        Directory.CreateDirectory(destPath);
                        await TarGzHelper.ExtractTarGzAsync(tarGzData, destPath, cancellationToken);
                        var extractedFiles = Directory.GetFiles(destPath, "*", SearchOption.AllDirectories).Length;
                        totalFiles += extractedFiles;
                        ConsoleUI.WriteBullet($"Repo '{repoName}': {extractedFiles} file(s)", ConsoleColor.Gray);
                    }
                    catch (Exception ex)
                    {
                        ConsoleUI.WriteBullet($"Repo '{repoName}': Failed - {ex.Message}", ConsoleColor.Red);
                        hasErrors = true;
                    }
                }
                else if (!success)
                {
                    ConsoleUI.WriteBullet($"Repo '{repoName}': {errorMessage}", ConsoleColor.Yellow);
                }
            }
        }
        else
        {
            ConsoleUI.WriteBullet($"Failed to list repos: {repoErrorMessage}", ConsoleColor.Yellow);
        }

        // 2. Download synthesized knowledge
        ConsoleUI.WriteInfo("Downloading synthesized knowledge...", ConsoleColor.Gray);
        var (skSuccess, skTarGzData, skErrorMessage) = await apiService.DownloadSynthesizedKnowledgeAsync();
        if (skSuccess && skTarGzData.Length > 0)
        {
            var destPath = string.IsNullOrWhiteSpace(specifiedPath)
                ? Path.Combine(basePath, SynthesizedKnowledgeFolderName)
                : Path.Combine(basePath, SynthesizedKnowledgeFolderName);

            try
            {
                Directory.CreateDirectory(destPath);
                await TarGzHelper.ExtractTarGzAsync(skTarGzData, destPath, cancellationToken);
                var extractedFiles = Directory.GetFiles(destPath, "*", SearchOption.AllDirectories).Length;
                totalFiles += extractedFiles;
                ConsoleUI.WriteBullet($"Synthesized knowledge: {extractedFiles} file(s)", ConsoleColor.Gray);
            }
            catch (Exception ex)
            {
                ConsoleUI.WriteBullet($"Synthesized knowledge: Failed - {ex.Message}", ConsoleColor.Red);
                hasErrors = true;
            }
        }
        else if (!skSuccess && !skErrorMessage.Contains("404") && !skErrorMessage.Contains("not exist", StringComparison.OrdinalIgnoreCase))
        {
            ConsoleUI.WriteBullet($"Synthesized knowledge: {skErrorMessage}", ConsoleColor.Yellow);
        }
        else
        {
            ConsoleUI.WriteBullet("Synthesized knowledge: No files found", ConsoleColor.Gray);
        }

        // 3. Optionally download session insights
        if (includeSessionInsights)
        {
            var threadDesc = threadId.HasValue ? $"thread {threadId}" : "all threads";
            ConsoleUI.WriteInfo($"Downloading session insights for {threadDesc}...", ConsoleColor.Gray);
            var (siSuccess, siTarGzData, siErrorMessage) = await apiService.DownloadSessionInsightsAsync(threadId);
            if (siSuccess && siTarGzData.Length > 0)
            {
                var destPath = string.IsNullOrWhiteSpace(specifiedPath)
                    ? Path.Combine(basePath, SessionInsightsFolderName, threadId?.ToString() ?? "")
                    : Path.Combine(basePath, SessionInsightsFolderName, threadId?.ToString() ?? "");

                try
                {
                    Directory.CreateDirectory(destPath);
                    await TarGzHelper.ExtractTarGzAsync(siTarGzData, destPath, cancellationToken);
                    var extractedFiles = Directory.GetFiles(destPath, "*", SearchOption.AllDirectories).Length;
                    totalFiles += extractedFiles;
                    ConsoleUI.WriteBullet($"Session insights: {extractedFiles} file(s)", ConsoleColor.Gray);
                }
                catch (Exception ex)
                {
                    ConsoleUI.WriteBullet($"Session insights: Failed - {ex.Message}", ConsoleColor.Red);
                    hasErrors = true;
                }
            }
            else if (!siSuccess && !siErrorMessage.Contains("404") && !siErrorMessage.Contains("not exist", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleUI.WriteBullet($"Session insights: {siErrorMessage}", ConsoleColor.Yellow);
            }
            else
            {
                ConsoleUI.WriteBullet("Session insights: No files found", ConsoleColor.Gray);
            }
        }

        Console.WriteLine();
        if (hasErrors)
        {
            ConsoleUI.WriteStatus(false, $"Sync completed with errors. {totalFiles} file(s) downloaded.");
            return 1;
        }
        else
        {
            ConsoleUI.WriteStatus(true, $"Workspace sync completed. {totalFiles} file(s) downloaded.");
            ConsoleUI.WriteKeyValue("Location", basePath, 0);
            return 0;
        }
    }

    #endregion

    #region Session Insights Handlers

    /// <summary>
    /// Handles the session-insights download command.
    /// </summary>
    public static async Task<int> HandleSessionInsightsDownloadCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting session-insights download command");

        var specifiedPath = parseResult.GetValue(WorkspaceCommandOptions.Memory.PathOption);
        var threadIdStr = parseResult.GetValue(WorkspaceCommandOptions.Memory.ThreadIdOption);

        Guid? threadId;
        try
        {
            threadId = WorkspaceCommandOptions.Memory.ParseThreadId(threadIdStr);
        }
        catch (ArgumentException ex)
        {
            ConsoleUI.WriteStatus(false, ex.Message);
            return 1;
        }

        // Determine destination path
        // If --path is provided, use it directly without appending subfolders
        // Otherwise, use default path with subfolders
        string destPath;
        if (!string.IsNullOrWhiteSpace(specifiedPath))
        {
            destPath = specifiedPath;
        }
        else
        {
            var localPath = WorkspaceCommandOptions.Memory.GetEffectivePath(null);
            if (threadId.HasValue)
            {
                destPath = Path.Combine(localPath, SessionInsightsFolderName, threadId.Value.ToString());
            }
            else
            {
                destPath = Path.Combine(localPath, SessionInsightsFolderName);
            }
        }

        DebugLogger.Debug("Parameters", $"Path: {destPath}, ThreadId: {threadId?.ToString() ?? "all"}");

        // Show what will be downloaded
        var threadDesc = threadId.HasValue ? $"thread '{threadId}'" : "all threads";
        ConsoleUI.WriteInfo($"Downloading session insights ({threadDesc}) to: {destPath}", ConsoleColor.Cyan);
        Console.WriteLine();

        // Download from server
        ConsoleUI.WriteInfo("Downloading...", ConsoleColor.Gray);
        using var apiService = new ApiService();
        var (success, tarGzData, errorMessage) = await apiService.DownloadSessionInsightsAsync(threadId);

        if (!success || tarGzData.Length == 0)
        {
            ConsoleUI.WriteStatus(false, errorMessage);
            return 1;
        }

        DebugLogger.Debug("Download", $"Received tar.gz archive: {tarGzData.Length:N0} bytes");
        ConsoleUI.WriteInfo($"Received {tarGzData.Length:N0} bytes", ConsoleColor.Gray);

        // Extract to local path
        ConsoleUI.WriteInfo("Extracting...", ConsoleColor.Gray);
        try
        {
            Directory.CreateDirectory(destPath);
            await TarGzHelper.ExtractTarGzAsync(tarGzData, destPath, cancellationToken);

            var extractedFiles = Directory.GetFiles(destPath, "*", SearchOption.AllDirectories).Length;

            Console.WriteLine();
            ConsoleUI.WriteStatus(true, $"Session insights ({threadDesc}) downloaded successfully ({extractedFiles} file(s)).");
            ConsoleUI.WriteKeyValue("Location", destPath, 0);
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Failed to extract archive: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the session-insights delete command.
    /// </summary>
    public static async Task<int> HandleSessionInsightsDeleteCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting session-insights delete command");

        var threadIdStr = parseResult.GetValue(WorkspaceCommandOptions.Memory.ThreadIdOption);

        Guid? threadId;
        try
        {
            threadId = WorkspaceCommandOptions.Memory.ParseThreadId(threadIdStr);
        }
        catch (ArgumentException ex)
        {
            ConsoleUI.WriteStatus(false, ex.Message);
            return 1;
        }

        DebugLogger.Debug("Parameters", $"ThreadId: {threadId?.ToString() ?? "all"}");

        // Confirm deletion
        var threadDesc = threadId.HasValue ? $"thread '{threadId}'" : "all threads";
        ConsoleUI.WriteInfo($"Deleting session insights ({threadDesc}) from remote agent...", ConsoleColor.Cyan);
        Console.WriteLine();

        // Delete from server
        ConsoleUI.WriteInfo("Deleting...", ConsoleColor.Gray);
        using var apiService = new ApiService();
        var (success, response) = await apiService.DeleteSessionInsightsAsync(threadId);

        if (success)
        {
            Console.WriteLine();
            ConsoleUI.WriteStatus(true, $"Session insights ({threadDesc}) deleted successfully.");
            ConsoleUI.WriteInfo(response, ConsoleColor.Gray);
            return 0;
        }
        else
        {
            Console.WriteLine();
            ConsoleUI.WriteStatus(false, response);
            return 1;
        }
    }

    #endregion

    #region Synthesized Knowledge Handlers

    /// <summary>
    /// Handles the synthesized-knowledge upload command.
    /// </summary>
    public static async Task<int> HandleSynthesizedKnowledgeUploadCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting synthesized-knowledge upload command");

        var specifiedPath = parseResult.GetValue(WorkspaceCommandOptions.Memory.PathOption);

        // Determine source path
        // If --path is provided, use it directly without appending subfolders
        // Otherwise, use default path with subfolders
        string sourcePath;
        if (!string.IsNullOrWhiteSpace(specifiedPath))
        {
            sourcePath = specifiedPath;
        }
        else
        {
            var localPath = WorkspaceCommandOptions.Memory.GetEffectivePath(null);
            sourcePath = Path.Combine(localPath, SynthesizedKnowledgeFolderName);
        }

        DebugLogger.Debug("Parameters", $"Path: {sourcePath}");

        // Validate path exists
        if (!Directory.Exists(sourcePath))
        {
            ConsoleUI.WriteStatus(false, $"Synthesized knowledge directory not found: {sourcePath}");
            return 1;
        }

        // Count files
        var fileCount = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories).Length;
        if (fileCount == 0)
        {
            ConsoleUI.WriteStatus(false, "No files found in synthesized knowledge directory.");
            return 1;
        }

        // Show what will be uploaded
        ConsoleUI.WriteInfo($"Uploading synthesized knowledge from: {sourcePath}", ConsoleColor.Cyan);
        ConsoleUI.WriteBullet($"{fileCount} file(s) to upload", ConsoleColor.Gray);
        Console.WriteLine();

        // Create tar.gz archive
        ConsoleUI.WriteInfo("Creating archive...", ConsoleColor.Gray);
        byte[] tarGzData;
        try
        {
            tarGzData = await TarGzHelper.CreateTarGzFromFoldersAsync(sourcePath, ["."], cancellationToken);
            DebugLogger.Debug("Archive", $"Created tar.gz archive: {tarGzData.Length:N0} bytes");
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Failed to create archive: {ex.Message}");
            return 1;
        }

        // Upload to server
        ConsoleUI.WriteInfo($"Uploading ({tarGzData.Length:N0} bytes)...", ConsoleColor.Gray);
        using var apiService = new ApiService();
        var (success, response) = await apiService.UploadSynthesizedKnowledgeAsync(tarGzData);

        if (success)
        {
            Console.WriteLine();
            ConsoleUI.WriteStatus(true, "Synthesized knowledge uploaded successfully.");
            ConsoleUI.WriteInfo(response, ConsoleColor.Gray);
            return 0;
        }
        else
        {
            Console.WriteLine();
            ConsoleUI.WriteStatus(false, response);
            return 1;
        }
    }

    /// <summary>
    /// Handles the synthesized-knowledge download command.
    /// </summary>
    public static async Task<int> HandleSynthesizedKnowledgeDownloadCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting synthesized-knowledge download command");

        var specifiedPath = parseResult.GetValue(WorkspaceCommandOptions.Memory.PathOption);

        // Determine destination path
        // If --path is provided, use it directly without appending subfolders
        // Otherwise, use default path with subfolders
        string destPath;
        if (!string.IsNullOrWhiteSpace(specifiedPath))
        {
            destPath = specifiedPath;
        }
        else
        {
            var localPath = WorkspaceCommandOptions.Memory.GetEffectivePath(null);
            destPath = Path.Combine(localPath, SynthesizedKnowledgeFolderName);
        }

        DebugLogger.Debug("Parameters", $"Path: {destPath}");

        // Show what will be downloaded
        ConsoleUI.WriteInfo($"Downloading synthesized knowledge to: {destPath}", ConsoleColor.Cyan);
        Console.WriteLine();

        // Download from server
        ConsoleUI.WriteInfo("Downloading...", ConsoleColor.Gray);
        using var apiService = new ApiService();
        var (success, tarGzData, errorMessage) = await apiService.DownloadSynthesizedKnowledgeAsync();

        if (!success || tarGzData.Length == 0)
        {
            ConsoleUI.WriteStatus(false, errorMessage);
            return 1;
        }

        DebugLogger.Debug("Download", $"Received tar.gz archive: {tarGzData.Length:N0} bytes");
        ConsoleUI.WriteInfo($"Received {tarGzData.Length:N0} bytes", ConsoleColor.Gray);

        // Extract to local path
        ConsoleUI.WriteInfo("Extracting...", ConsoleColor.Gray);
        try
        {
            Directory.CreateDirectory(destPath);
            await TarGzHelper.ExtractTarGzAsync(tarGzData, destPath, cancellationToken);

            var extractedFiles = Directory.GetFiles(destPath, "*", SearchOption.AllDirectories).Length;

            Console.WriteLine();
            ConsoleUI.WriteStatus(true, $"Synthesized knowledge downloaded successfully ({extractedFiles} file(s)).");
            ConsoleUI.WriteKeyValue("Location", destPath, 0);
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Failed to extract archive: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the synthesized-knowledge delete command.
    /// </summary>
    public static async Task<int> HandleSynthesizedKnowledgeDeleteCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting synthesized-knowledge delete command");

        // Confirm deletion
        ConsoleUI.WriteInfo("Deleting synthesized knowledge from remote agent...", ConsoleColor.Cyan);
        Console.WriteLine();

        // Delete from server
        ConsoleUI.WriteInfo("Deleting...", ConsoleColor.Gray);
        using var apiService = new ApiService();
        var (success, response) = await apiService.DeleteSynthesizedKnowledgeAsync();

        if (success)
        {
            Console.WriteLine();
            ConsoleUI.WriteStatus(true, "Synthesized knowledge deleted successfully.");
            ConsoleUI.WriteInfo(response, ConsoleColor.Gray);
            return 0;
        }
        else
        {
            Console.WriteLine();
            ConsoleUI.WriteStatus(false, response);
            return 1;
        }
    }

    #endregion
}
