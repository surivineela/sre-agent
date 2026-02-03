// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Formats.Tar;
using System.IO.Compression;
using Agent.Common.Services;
using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Web.Authorization;
using Microsoft.AspNetCore.Mvc;
using ArmOperations = Agent.Core.Constants.ArmOperations;

namespace Agent.Web.Controllers.v1;

/// <summary>
/// Response model for workspace memory upload operation.
/// </summary>
public record WorkspaceMemoryUploadResponse(
    string Message,
    int UploadedFiles,
    long TotalSize);

/// <summary>
/// Response model for workspace memory delete operation.
/// </summary>
public record WorkspaceMemoryDeleteResponse(
    string Message,
    int DeletedFiles);

/// <summary>
/// Response model for workspace memory file listing.
/// </summary>
public record WorkspaceMemoryFileInfo(
    string Path,
    long Size,
    DateTime LastModified);

/// <summary>
/// Response model for workspace memory list operation.
/// </summary>
public record WorkspaceMemoryListResponse(
    List<WorkspaceMemoryFileInfo> Files,
    int TotalCount,
    long TotalSize);

/// <summary>
/// Controller for workspace memory upload, download, and delete operations.
/// Provides separate endpoints for each memory type: repo-instructions, session-insights, synthesized-knowledge.
/// Syncs files to both local storage and blob storage.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class WorkspaceMemoryController : ControllerBase
{
    // Memory folder constants (same as in AgentFileStorageService)
    private const string RepoInstructionsFolderName = ".github";
    private const string SessionInsightsFolderName = "sessionInsights";
    private const string SynthesizedKnowledgeFolderName = "synthesizedKnowledge";

    private readonly ILogger<WorkspaceMemoryController> logger;
    private readonly IAgentFileStorageService agentFileStorageService;
    private readonly Lazy<Task<SandboxPaths>> _sandboxPaths;

    public WorkspaceMemoryController(
        ILogger<WorkspaceMemoryController> logger,
        IAgentFileStorageService agentFileStorageService,
        ISandboxPaths sandboxPaths)
    {
        this.logger = logger;
        this.agentFileStorageService = agentFileStorageService;
        _sandboxPaths = new Lazy<Task<SandboxPaths>>(
            () => sandboxPaths.GetSandboxPathsAsync(),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Helper to get the local memory path asynchronously.
    /// </summary>
    private async Task<string> GetLocalMemoryPathAsync()
        => (await _sandboxPaths.Value).MemoriesPath;

    #region Repo Instructions (.github)

    /// <summary>
    /// Uploads repo instruction files from a tar.gz archive.
    /// Extracts to local memories folder and syncs to blob storage.
    /// </summary>
    /// <param name="repo">Repository name (required)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload result with count and size of uploaded files</returns>
    [HttpPost("repo-instructions")]
    [AuthorizeArmOperation(ArmOperations.AgentMemoryWriteActionId)]
    [Consumes("application/gzip", "application/x-gzip", "application/octet-stream")]
    [ProducesResponseType(typeof(WorkspaceMemoryUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadRepoInstructions(
        [FromQuery] string repo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repo))
        {
            return BadRequest(new { error = "Repository name is required" });
        }

        if (Request.ContentLength == null || Request.ContentLength == 0)
        {
            return BadRequest(new { error = "No content provided" });
        }

        logger.LogInternalInformation(
            "Received repo instructions upload request. Repo: {Repo}, ContentLength: {ContentLength}",
            repo, Request.ContentLength);

        try
        {
            var localMemoryPath = await GetLocalMemoryPathAsync();

            // Local path: {MemoriesPath}/{repo}/.github
            var (uploadedFiles, totalSize) = await ExtractTarGzToFolderAsync(
                Path.Combine(localMemoryPath, repo, RepoInstructionsFolderName),
                cancellationToken);

            // Sync to blob storage
            await agentFileStorageService.UploadWorkspaceRepoInstructionsAsync(repo, cancellationToken);

            logger.LogInternalInformation(
                "Repo instructions upload completed. Repo: {Repo}, Files: {FileCount}, Size: {TotalSize} bytes",
                repo, uploadedFiles, totalSize);

            return Ok(new WorkspaceMemoryUploadResponse(
                Message: $"Repo instructions for '{repo}' uploaded successfully",
                UploadedFiles: uploadedFiles,
                TotalSize: totalSize));
        }
        catch (InvalidDataException ex)
        {
            logger.LogInternalWarning(ex, "Invalid tar.gz format in upload request");
            return BadRequest(new { error = "Invalid tar.gz format" });
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Failed to upload repo instructions for {Repo}", repo);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to upload repo instructions" });
        }
    }

    /// <summary>
    /// Downloads repo instruction files as a tar.gz archive.
    /// </summary>
    /// <param name="repo">Repository name (required)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>tar.gz archive of repo instruction files</returns>
    [HttpGet("repo-instructions")]
    [AuthorizeArmOperation(ArmOperations.AgentMemoryReadActionId)]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DownloadRepoInstructions(
        [FromQuery] string repo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repo))
        {
            return BadRequest(new { error = "Repository name is required" });
        }

        logger.LogInternalInformation("Received repo instructions download request. Repo: {Repo}", repo);

        try
        {
            var localMemoryPath = await GetLocalMemoryPathAsync();

            // Local path: {MemoriesPath}/{repo}/.github
            var folderPath = Path.Combine(localMemoryPath, repo, RepoInstructionsFolderName);

            logger.LogInternalInformation("Downloading from blob storage", repo);
            var downloadedCount = await agentFileStorageService.DownloadWorkspaceRepoInstructionsAsync(repo, cancellationToken);
            logger.LogInternalInformation("Downloaded {Count} repo instruction files from blob storage for {Repo}", downloadedCount, repo);

            // Check after download
            if (!Directory.Exists(folderPath))
            {
                return NotFound(new { error = $"Repo instructions folder for '{repo}' does not exist" });
            }

            var memoryStream = await CreateTarGzFromFolderAsync(folderPath, RepoInstructionsFolderName, cancellationToken);

            logger.LogInternalInformation(
                "Repo instructions download completed. Repo: {Repo}, Size: {Size} bytes",
                repo, memoryStream.Length);

            return File(memoryStream, "application/gzip", $"repo-instructions-{repo}.tar.gz");
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Failed to download repo instructions for {Repo}", repo);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to download repo instructions" });
        }
    }

    /// <summary>
    /// Deletes repo instruction files from local storage and blob storage.
    /// </summary>
    /// <param name="repo">Repository name (required)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Delete result with count of deleted files</returns>
    [HttpDelete("repo-instructions")]
    [AuthorizeArmOperation(ArmOperations.AgentMemoryWriteActionId)]
    [ProducesResponseType(typeof(WorkspaceMemoryDeleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteRepoInstructions(
        [FromQuery] string repo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repo))
        {
            return BadRequest(new { error = "Repository name is required" });
        }

        logger.LogInternalInformation("Received repo instructions delete request. Repo: {Repo}", repo);

        try
        {
            var deletedCount = await agentFileStorageService.DeleteWorkspaceRepoInstructionsAsync(repo, cancellationToken);

            logger.LogInternalInformation(
                "Repo instructions delete completed. Repo: {Repo}, DeletedFiles: {DeletedCount}",
                repo, deletedCount);

            return Ok(new WorkspaceMemoryDeleteResponse(
                Message: $"Repo instructions for '{repo}' deleted successfully",
                DeletedFiles: deletedCount));
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Failed to delete repo instructions for {Repo}", repo);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to delete repo instructions" });
        }
    }

    #endregion

    #region Session Insights

    /// <summary>
    /// Downloads session insight files as a tar.gz archive.
    /// </summary>
    /// <param name="threadId">Thread ID (optional - if not provided, downloads all session insights)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>tar.gz archive of session insight files</returns>
    [HttpGet("session-insights")]
    [AuthorizeArmOperation(ArmOperations.AgentMemoryReadActionId)]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DownloadSessionInsights(
        [FromQuery] Guid? threadId = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInternalInformation("Received session insights download request. ThreadId: {ThreadId}", threadId?.ToString() ?? "all");

        try
        {
            var localMemoryPath = await GetLocalMemoryPathAsync();
            string folderPath;
            string archiveName;

            if (threadId.HasValue)
            {
                folderPath = Path.Combine(localMemoryPath, SessionInsightsFolderName, threadId.Value.ToString());
                archiveName = $"session-insights-{threadId}.tar.gz";
            }
            else
            {
                folderPath = Path.Combine(localMemoryPath, SessionInsightsFolderName);
                archiveName = "session-insights.tar.gz";
            }

            // If local folder doesn't exist, try downloading from blob storage (without saving locally)
            if (!Directory.Exists(folderPath))
            {
                logger.LogInternalInformation("Local session insights not found for {ThreadId}, attempting to download from blob storage", threadId?.ToString() ?? "all");
                var blobStream = await agentFileStorageService.DownloadWorkspaceSessionInsightsToStreamAsync(threadId, cancellationToken);

                if (blobStream != null)
                {
                    logger.LogInternalInformation(
                        "Session insights download from blob completed. ThreadId: {ThreadId}, Size: {Size} bytes",
                        threadId?.ToString() ?? "all", blobStream.Length);

                    return File(blobStream, "application/gzip", archiveName);
                }

                return NotFound(new { error = "Session insights folder does not exist" });
            }

            var memoryStream = await CreateTarGzFromFolderAsync(folderPath, SessionInsightsFolderName, cancellationToken);

            logger.LogInternalInformation(
                "Session insights download completed. ThreadId: {ThreadId}, Size: {Size} bytes",
                threadId?.ToString() ?? "all", memoryStream.Length);

            return File(memoryStream, "application/gzip", archiveName);
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Failed to download session insights for ThreadId: {ThreadId}", threadId?.ToString() ?? "all");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to download session insights" });
        }
    }

    /// <summary>
    /// Deletes session insight files from local storage and blob storage.
    /// </summary>
    /// <param name="threadId">Thread ID (optional - if not provided, deletes all session insights)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Delete result with count of deleted files</returns>
    [HttpDelete("session-insights")]
    [AuthorizeArmOperation(ArmOperations.AgentMemoryWriteActionId)]
    [ProducesResponseType(typeof(WorkspaceMemoryDeleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteSessionInsights(
        [FromQuery] Guid? threadId = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInternalInformation("Received session insights delete request. ThreadId: {ThreadId}", threadId?.ToString() ?? "all");

        try
        {
            var deletedCount = await agentFileStorageService.DeleteWorkspaceSessionInsightsAsync(threadId, cancellationToken);

            logger.LogInternalInformation(
                "Session insights delete completed. ThreadId: {ThreadId}, DeletedFiles: {DeletedCount}",
                threadId?.ToString() ?? "all", deletedCount);

            return Ok(new WorkspaceMemoryDeleteResponse(
                Message: threadId.HasValue
                    ? $"Session insights for thread '{threadId}' deleted successfully"
                    : "All session insights deleted successfully",
                DeletedFiles: deletedCount));
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Failed to delete session insights for ThreadId: {ThreadId}", threadId?.ToString() ?? "all");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to delete session insights" });
        }
    }

    #endregion

    #region Synthesized Knowledge

    /// <summary>
    /// Uploads synthesized knowledge files from a tar.gz archive.
    /// Extracts to local memories folder and syncs to blob storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload result with count and size of uploaded files</returns>
    [HttpPost("synthesized-knowledge")]
    [AuthorizeArmOperation(ArmOperations.AgentMemoryWriteActionId)]
    [Consumes("application/gzip", "application/x-gzip", "application/octet-stream")]
    [ProducesResponseType(typeof(WorkspaceMemoryUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadSynthesizedKnowledge(
        CancellationToken cancellationToken = default)
    {
        if (Request.ContentLength == null || Request.ContentLength == 0)
        {
            return BadRequest(new { error = "No content provided" });
        }

        logger.LogInternalInformation(
            "Received synthesized knowledge upload request. ContentLength: {ContentLength}",
            Request.ContentLength);

        try
        {
            var localMemoryPath = await GetLocalMemoryPathAsync();
            var destPath = Path.Combine(localMemoryPath, SynthesizedKnowledgeFolderName);

            var (uploadedFiles, totalSize) = await ExtractTarGzToFolderAsync(destPath, cancellationToken);

            // Sync to blob storage
            await agentFileStorageService.UploadWorkspaceSynthesizedKnowledgeAsync(cancellationToken);

            logger.LogInternalInformation(
                "Synthesized knowledge upload completed. Files: {FileCount}, Size: {TotalSize} bytes",
                uploadedFiles, totalSize);

            return Ok(new WorkspaceMemoryUploadResponse(
                Message: "Synthesized knowledge uploaded successfully",
                UploadedFiles: uploadedFiles,
                TotalSize: totalSize));
        }
        catch (InvalidDataException ex)
        {
            logger.LogInternalWarning(ex, "Invalid tar.gz format in upload request");
            return BadRequest(new { error = "Invalid tar.gz format" });
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Failed to upload synthesized knowledge");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to upload synthesized knowledge" });
        }
    }

    /// <summary>
    /// Downloads synthesized knowledge files as a tar.gz archive.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>tar.gz archive of synthesized knowledge files</returns>
    [HttpGet("synthesized-knowledge")]
    [AuthorizeArmOperation(ArmOperations.AgentMemoryReadActionId)]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DownloadSynthesizedKnowledge(
        CancellationToken cancellationToken = default)
    {
        logger.LogInternalInformation("Received synthesized knowledge download request");

        try
        {
            var localMemoryPath = await GetLocalMemoryPathAsync();
            var folderPath = Path.Combine(localMemoryPath, SynthesizedKnowledgeFolderName);

            logger.LogInternalInformation("Downloading from blob storage");
            var downloadedCount = await agentFileStorageService.DownloadWorkspaceSynthesizedKnowledgeAsync(cancellationToken);
            logger.LogInternalInformation("Downloaded {Count} synthesized knowledge files from blob storage", downloadedCount);

            // Check after download
            if (!Directory.Exists(folderPath))
            {
                return NotFound(new { error = "Synthesized knowledge folder does not exist" });
            }

            var memoryStream = await CreateTarGzFromFolderAsync(folderPath, SynthesizedKnowledgeFolderName, cancellationToken);

            logger.LogInternalInformation(
                "Synthesized knowledge download completed. Size: {Size} bytes",
                memoryStream.Length);

            return File(memoryStream, "application/gzip", "synthesized-knowledge.tar.gz");
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Failed to download synthesized knowledge");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to download synthesized knowledge" });
        }
    }

    /// <summary>
    /// Deletes synthesized knowledge files from local storage and blob storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Delete result with count of deleted files</returns>
    [HttpDelete("synthesized-knowledge")]
    [AuthorizeArmOperation(ArmOperations.AgentMemoryWriteActionId)]
    [ProducesResponseType(typeof(WorkspaceMemoryDeleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteSynthesizedKnowledge(
        CancellationToken cancellationToken = default)
    {
        logger.LogInternalInformation("Received synthesized knowledge delete request");

        try
        {
            var deletedCount = await agentFileStorageService.DeleteWorkspaceSynthesizedKnowledgeAsync(cancellationToken);

            logger.LogInternalInformation(
                "Synthesized knowledge delete completed. DeletedFiles: {DeletedCount}",
                deletedCount);

            return Ok(new WorkspaceMemoryDeleteResponse(
                Message: "Synthesized knowledge deleted successfully",
                DeletedFiles: deletedCount));
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Failed to delete synthesized knowledge");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to delete synthesized knowledge" });
        }
    }

    #endregion

    #region List Endpoints

    /// <summary>
    /// Lists all workspace memory files.
    /// </summary>
    /// <param name="type">Memory type filter (repo-instructions, session-insights, synthesized-knowledge)</param>
    /// <param name="repo">Repository name (only for repo-instructions)</param>
    /// <param name="threadId">Thread ID (only for session-insights)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of memory files with metadata</returns>
    [HttpGet("list")]
    [AuthorizeArmOperation(ArmOperations.AgentMemoryReadActionId)]
    [ProducesResponseType(typeof(WorkspaceMemoryListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> List(
        [FromQuery] string? type = null,
        [FromQuery] string? repo = null,
        [FromQuery] Guid? threadId = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInternalInformation(
            "Received workspace memory list request. Type: {Type}, Repo: {Repo}, ThreadId: {ThreadId}",
            type ?? "all", repo ?? "all", threadId?.ToString() ?? "all");

        try
        {
            var localMemoryPath = await GetLocalMemoryPathAsync();
            var files = new List<WorkspaceMemoryFileInfo>();
            long totalSize = 0;

            if (!Directory.Exists(localMemoryPath))
            {
                return Ok(new WorkspaceMemoryListResponse(
                    Files: files,
                    TotalCount: 0,
                    TotalSize: 0));
            }

            // Determine which folders to include
            var foldersToScan = new List<string>();

            if (string.IsNullOrEmpty(type) || type == "repo-instructions")
            {
                if (!string.IsNullOrEmpty(repo))
                {
                    // New path structure: {repo}/.github
                    foldersToScan.Add(Path.Combine(repo, RepoInstructionsFolderName));
                }
                else
                {
                    // Scan all repo folders for .github subdirectories
                    if (Directory.Exists(localMemoryPath))
                    {
                        foreach (var repoDir in Directory.GetDirectories(localMemoryPath))
                        {
                            var githubDir = Path.Combine(repoDir, RepoInstructionsFolderName);
                            if (Directory.Exists(githubDir))
                            {
                                var repoName = Path.GetFileName(repoDir);
                                foldersToScan.Add(Path.Combine(repoName, RepoInstructionsFolderName));
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(type) || type == "session-insights")
            {
                if (threadId.HasValue)
                {
                    foldersToScan.Add(Path.Combine(SessionInsightsFolderName, threadId.Value.ToString()));
                }
                else
                {
                    foldersToScan.Add(SessionInsightsFolderName);
                }
            }

            if (string.IsNullOrEmpty(type) || type == "synthesized-knowledge")
            {
                foldersToScan.Add(SynthesizedKnowledgeFolderName);
            }

            foreach (var folder in foldersToScan)
            {
                var folderPath = Path.Combine(localMemoryPath, folder);
                if (!Directory.Exists(folderPath))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fileInfo = new FileInfo(file);
                    var relativePath = Path.GetRelativePath(localMemoryPath, file).Replace('\\', '/');

                    files.Add(new WorkspaceMemoryFileInfo(
                        Path: relativePath,
                        Size: fileInfo.Length,
                        LastModified: fileInfo.LastWriteTimeUtc));

                    totalSize += fileInfo.Length;
                }
            }

            logger.LogInternalInformation(
                "Workspace memory list completed. Files: {FileCount}, TotalSize: {TotalSize} bytes",
                files.Count, totalSize);

            return Ok(new WorkspaceMemoryListResponse(
                Files: files,
                TotalCount: files.Count,
                TotalSize: totalSize));
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Failed to list workspace memory");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to list workspace memory" });
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Extracts a tar.gz stream to a destination folder.
    /// </summary>
    private async Task<(int FileCount, long TotalSize)> ExtractTarGzToFolderAsync(
        string destPath,
        CancellationToken cancellationToken)
    {
        // Create a temporary directory to extract files
        var tempDir = Path.Combine(Path.GetTempPath(), $"workspace-memory-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Extract tar.gz to temp directory
            using var gzipStream = new GZipStream(Request.Body, CompressionMode.Decompress);
            await TarFile.ExtractToDirectoryAsync(gzipStream, tempDir, overwriteFiles: true, cancellationToken);

            // Ensure destination directory exists
            Directory.CreateDirectory(destPath);

            var uploadedFiles = 0;
            long totalSize = 0;

            // Copy files from temp directory to destination
            foreach (var file in Directory.EnumerateFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(tempDir, file);
                var destFile = Path.Combine(destPath, relativePath);

                // Ensure destination directory exists
                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                // Copy file
                System.IO.File.Copy(file, destFile, overwrite: true);

                var fileInfo = new FileInfo(destFile);
                totalSize += fileInfo.Length;
                uploadedFiles++;

                logger.LogInternalDebug("Extracted file: {FilePath}", destFile);
            }

            return (uploadedFiles, totalSize);
        }
        finally
        {
            // Clean up temp directory
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                logger.LogInternalWarning(ex, "Failed to clean up temp directory: {TempDir}", tempDir);
            }
        }
    }

    /// <summary>
    /// Creates a tar.gz archive from a folder.
    /// </summary>
    private async Task<MemoryStream> CreateTarGzFromFolderAsync(
        string folderPath,
        string rootFolderName,
        CancellationToken cancellationToken)
    {
        var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
        await using (var tarWriter = new TarWriter(gzipStream, leaveOpen: true))
        {
            foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Create relative path from folder root, preserving structure
                var relativePath = Path.GetRelativePath(folderPath, file).Replace('\\', '/');

                await tarWriter.WriteEntryAsync(file, relativePath, cancellationToken);
            }
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    #endregion
}
