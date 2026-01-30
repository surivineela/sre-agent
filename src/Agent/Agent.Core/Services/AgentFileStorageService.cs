// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Common.Services;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Core.Services;

/// <summary>
/// Service for managing agent files including thread files, tool outputs, and memories with local caching.
/// Uses NullRemoteFileStorage when remote storage is not configured, so all remote operations are safe to call.
/// </summary>
public class AgentFileStorageService : IAgentFileStorageService
{
    // Memories constants
    internal const string MemoriesContainerName = "memories";
    internal const string RepoInstructionsFolderName = ".github";
    internal const string SessionInsightsFolderName = "sessionInsights";
    internal const string SynthesizedKnowledgeFolderName = "synthesizedKnowledge";

    private readonly IRemoteFileStorage _remoteStorage;
    private readonly ToolOutputSettings _settings;
    private readonly ILogger<AgentFileStorageService> _logger;
    private readonly string _localToolOutputPath;
    private readonly string _localThreadFilesPath;
    private readonly string _localSandboxMemoryPath;
    private readonly ConcurrentDictionary<string, DateTime> _memoryFileLastUploaded = new();

    public AgentFileStorageService(
        IOptions<ToolOutputSettings> settings,
        ILogger<AgentFileStorageService> logger,
        IRemoteFileStorage remoteStorage)
    {
        _remoteStorage = remoteStorage;
        _settings = settings.Value;
        _logger = logger;

        // Use configured path if available, otherwise fall back to temp directory
        var basePath = !string.IsNullOrEmpty(_settings.StoragePath)
            ? _settings.StoragePath
            : Path.Combine(Path.GetTempPath(), "SREAgent");

        _localToolOutputPath = Path.Combine(basePath, "ToolOutput");
        _localThreadFilesPath = Path.Combine(basePath, "ThreadFiles");
        _localSandboxMemoryPath = new LocalSandboxPaths().SandboxPaths.MemoriesPath;

        // Ensure local directories exist
        try
        {
            Directory.CreateDirectory(_localToolOutputPath);
            Directory.CreateDirectory(_localThreadFilesPath);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to create local storage directories at {BasePath}", basePath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> UploadThreadFileAsync(
        Guid threadId,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        if (content == null || content.Length == 0)
        {
            throw new ArgumentException("Content cannot be null or empty.", nameof(content));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));
        }

        // Sanitize filename to prevent path traversal
        var sanitizedFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(sanitizedFileName))
        {
            throw new ArgumentException("Invalid file name.", nameof(fileName));
        }

        // Create blob path: {threadId}/{fileName}
        var blobPath = $"{threadId}/{sanitizedFileName}";

        // Always save locally first
        var threadDir = Path.Combine(_localThreadFilesPath, threadId.ToString());
        Directory.CreateDirectory(threadDir);
        var localFilePath = Path.Combine(threadDir, sanitizedFileName);

        try
        {
            await File.WriteAllBytesAsync(localFilePath, content, cancellationToken);
            _logger.LogInternalInformation(
                "Saved thread file locally: {LocalPath}, Size: {Size} bytes",
                localFilePath, content.Length);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to save thread file locally: {LocalPath}", localFilePath);
            throw;
        }

        // Upload to remote storage (no-op if not configured)
        try
        {
            await _remoteStorage.UploadAsync(
                _settings.ThreadFilesContainerName,
                blobPath,
                content,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to upload thread file to remote storage: {BlobPath}", blobPath);
            // Don't fail - local copy is available
        }

        return blobPath;
    }

    /// <inheritdoc />
    public async Task<string?> DownloadThreadFileAsync(
        Guid threadId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));
        }

        // Sanitize filename to prevent path traversal
        var sanitizedFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(sanitizedFileName))
        {
            throw new ArgumentException("Invalid file name.", nameof(fileName));
        }

        // Check local cache first
        var threadDir = Path.Combine(_localThreadFilesPath, threadId.ToString());

        // Validate path security (prevents path traversal and symlink attacks)
        if (!PathSecurityHelper.TryGetSafeFilePath(threadDir, sanitizedFileName, out var localFilePath) || localFilePath == null)
        {
            _logger.LogInternalWarning("Path traversal or symlink attack detected for thread file: {ThreadId}/{FileName}", threadId, fileName);
            return null;
        }

        if (File.Exists(localFilePath))
        {
            _logger.LogInternalDebug("Thread file found locally: {LocalPath}", localFilePath);
            return localFilePath;
        }

        // Ensure thread directory exists for remote download
        Directory.CreateDirectory(threadDir);

        // Try remote storage (no-op returns false if not configured)
        var blobPath = $"{threadId}/{sanitizedFileName}";
        var downloaded = await _remoteStorage.DownloadAsync(
            _settings.ThreadFilesContainerName,
            blobPath,
            localFilePath,
            cancellationToken);

        if (downloaded)
        {
            _logger.LogInternalDebug("Thread file downloaded from remote: {LocalPath}", localFilePath);
            return localFilePath;
        }

        _logger.LogInternalWarning("Thread file not found: {ThreadId}/{FileName}", threadId, sanitizedFileName);
        return null;
    }

    /// <inheritdoc />
    public async Task<string> SaveToolOutputAsync(
        Guid threadId,
        string toolName,
        string content,
        string extension,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be null or empty.", nameof(content));
        }

        // Add dot prefix to file extension if not present
        if (!extension.StartsWith("."))
        {
            extension = $".{extension}";
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var fileKey = $"{threadId}-{timestamp}{extension}";
        var lineCount = content.Split('\n').Length;
        var contentLength = content.Length;

        _logger.LogInternalInformation(
            "Saving tool output for thread {ThreadId}, tool {ToolName}, fileKey {FileKey}, lines {LineCount}, length {Length}, extension {Extension}",
            threadId, toolName, fileKey, lineCount, contentLength, extension);

        // Always save locally first
        var localFilePath = Path.Combine(_localToolOutputPath, fileKey);
        try
        {
            await File.WriteAllTextAsync(localFilePath, content, cancellationToken);
            _logger.LogInternalInformation("Saved tool output locally: {FilePath}", localFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to save tool output locally for thread {ThreadId}, tool {ToolName}", threadId, toolName);
            throw;
        }

        // Upload to remote storage (no-op if not configured)
        try
        {
            await _remoteStorage.UploadAsync(
                _settings.BlobStorageContainerName,
                fileKey,
                content,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to upload tool output to remote storage: {FileKey}", fileKey);
            // Don't fail - local copy is available
        }

        return fileKey;
    }

    /// <inheritdoc />
    public async Task<string?> GetToolOutputAsync(string fileKey, string? threadId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileKey))
        {
            return null;
        }

        try
        {
            // Sanitize fileKey to prevent path traversal and glob pattern injection
            var sanitizedFileKey = Path.GetFileName(fileKey);

            if (string.IsNullOrWhiteSpace(sanitizedFileKey) ||
                sanitizedFileKey.Contains("..") ||
                sanitizedFileKey.IndexOfAny(new[] { '*', '?' }) >= 0)
            {
                _logger.LogInternalWarning("Invalid fileKey detected: {FileKey}", fileKey);
                return null;
            }

            // Validate path security (prevents path traversal and symlink attacks)
            if (!PathSecurityHelper.TryGetSafeFilePath(_localToolOutputPath, sanitizedFileKey, out var toolOutputFilePath) || toolOutputFilePath == null)
            {
                _logger.LogInternalWarning("Path traversal or symlink attack detected for {FileKey}", fileKey);
                return null;
            }

            // Check tool output directory first
            if (File.Exists(toolOutputFilePath))
            {
                _logger.LogInternalDebug("Tool output file found locally: {FilePath}", toolOutputFilePath);
                return toolOutputFilePath;
            }

            // Try to download from remote storage (no-op returns false if not configured)
            var downloaded = await _remoteStorage.DownloadAsync(
                _settings.BlobStorageContainerName,
                sanitizedFileKey,
                toolOutputFilePath,
                cancellationToken);

            if (downloaded)
            {
                _logger.LogInternalInformation("Downloaded tool output from remote storage: {FileKey}", fileKey);
                return toolOutputFilePath;
            }

            _logger.LogInternalWarning("File not found for {FileKey}", fileKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error checking if file exists: {FileKey}", fileKey);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<int> CleanupThreadFilesAsync(
        Guid threadId,
        CancellationToken cancellationToken = default)
    {
        var deletedCount = 0;

        // Clean up local thread files
        var threadDir = Path.Combine(_localThreadFilesPath, threadId.ToString());
        if (Directory.Exists(threadDir))
        {
            try
            {
                var files = Directory.GetFiles(threadDir);
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalWarning(ex, "Failed to delete local thread file: {FilePath}", file);
                    }
                }

                // Try to remove the directory
                try
                {
                    Directory.Delete(threadDir);
                }
                catch
                {
                    // Ignore - directory might not be empty due to timing
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error cleaning up local thread files for thread {ThreadId}", threadId);
            }
        }

        // Clean up local tool output files with thread prefix
        try
        {
            var threadPrefix = $"{threadId}-";
            var toolOutputFiles = Directory.EnumerateFiles(_localToolOutputPath, $"{threadId}-*.*", SearchOption.TopDirectoryOnly);
            foreach (var file in toolOutputFiles)
            {
                try
                {
                    File.Delete(file);
                    deletedCount++;
                    _logger.LogInternalDebug("Deleted local tool output file for thread {ThreadId}: {FilePath}", threadId, file);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(ex, "Failed to delete local tool output file: {FilePath}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error cleaning up local tool output files for thread {ThreadId}", threadId);
        }

        // Clean up remote storage (no-op returns 0 if not configured)
        try
        {
            // Clean up thread files in remote storage
            var threadFilesDeleted = await _remoteStorage.DeleteByPrefixAsync(
                _settings.ThreadFilesContainerName,
                $"{threadId}/",
                cancellationToken);
            deletedCount += threadFilesDeleted;

            // Clean up tool outputs in remote storage
            var toolOutputsDeleted = await _remoteStorage.DeleteByPrefixAsync(
                _settings.BlobStorageContainerName,
                $"{threadId}-",
                cancellationToken);
            deletedCount += toolOutputsDeleted;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to cleanup remote storage for thread {ThreadId}", threadId);
        }

        _logger.LogInternalInformation("Cleanup completed for thread {ThreadId}. Deleted {DeletedCount} file(s).", threadId, deletedCount);
        return deletedCount;
    }

    #region Workspace Memory Methods

    /// <inheritdoc />
    public async Task<int> UploadWorkspaceRepoInstructionsAsync(
        string repoName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repoName))
        {
            throw new ArgumentException("Repository name cannot be null or empty.", nameof(repoName));
        }

        var localFolder = Path.Combine(_localSandboxMemoryPath, RepoInstructionsFolderName, repoName);
        if (!Directory.Exists(localFolder))
        {
            _logger.LogInternalDebug("Repo instructions folder does not exist: {Path}", localFolder);
            return 0;
        }

        var uploadedCount = 0;

        foreach (var filePath in Directory.EnumerateFiles(localFolder, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Check if file has changed since last upload
                var lastWriteTime = File.GetLastWriteTimeUtc(filePath);
                if (_memoryFileLastUploaded.TryGetValue(filePath, out var lastUploaded) && lastWriteTime <= lastUploaded)
                {
                    continue;
                }

                var relativePath = filePath.Substring(localFolder.Length).TrimStart(Path.DirectorySeparatorChar);
                var blobPath = $"{RepoInstructionsFolderName}/{repoName}/{relativePath.Replace(Path.DirectorySeparatorChar, '/')}";
                var content = await File.ReadAllBytesAsync(filePath, cancellationToken);

                await _remoteStorage.UploadAsync(
                    MemoriesContainerName,
                    blobPath,
                    content,
                    cancellationToken);

                _memoryFileLastUploaded[filePath] = lastWriteTime;
                uploadedCount++;
                _logger.LogInternalDebug("Uploaded repo instruction: {BlobPath}", blobPath);
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Failed to upload repo instruction: {FilePath}", filePath);
            }
        }

        if (uploadedCount > 0)
        {
            _logger.LogInternalInformation("Uploaded {Count} repo instruction file(s) for {RepoName}", uploadedCount, repoName);
        }

        return uploadedCount;
    }

    /// <inheritdoc />
    public async Task<int> UploadWorkspaceSessionInsightsAsync(
        Guid threadId,
        CancellationToken cancellationToken = default)
    {
        var localFolder = Path.Combine(_localSandboxMemoryPath, SessionInsightsFolderName, threadId.ToString());
        if (!Directory.Exists(localFolder))
        {
            _logger.LogInternalDebug("Session insights folder does not exist: {Path}", localFolder);
            return 0;
        }

        var uploadedCount = 0;

        foreach (var filePath in Directory.EnumerateFiles(localFolder, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Check if file has changed since last upload
                var lastWriteTime = File.GetLastWriteTimeUtc(filePath);
                if (_memoryFileLastUploaded.TryGetValue(filePath, out var lastUploaded) && lastWriteTime <= lastUploaded)
                {
                    continue;
                }

                var relativePath = filePath.Substring(localFolder.Length).TrimStart(Path.DirectorySeparatorChar);
                var blobPath = $"{SessionInsightsFolderName}/{threadId}/{relativePath.Replace(Path.DirectorySeparatorChar, '/')}";
                var content = await File.ReadAllBytesAsync(filePath, cancellationToken);

                await _remoteStorage.UploadAsync(
                    MemoriesContainerName,
                    blobPath,
                    content,
                    cancellationToken);

                _memoryFileLastUploaded[filePath] = lastWriteTime;
                uploadedCount++;
                _logger.LogInternalDebug("Uploaded session insights for thread {ThreadId}: {BlobPath}", threadId, blobPath);
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Failed to upload session insights for thread {ThreadId}: {FilePath}", threadId, filePath);
            }
        }

        if (uploadedCount > 0)
        {
            _logger.LogInternalInformation("Uploaded {Count} session insights file(s) for thread {ThreadId}", uploadedCount, threadId);
        }

        return uploadedCount;
    }

    /// <inheritdoc />
    public async Task<int> UploadWorkspaceSynthesizedKnowledgeAsync(
        CancellationToken cancellationToken = default)
    {
        var localFolder = Path.Combine(_localSandboxMemoryPath, SynthesizedKnowledgeFolderName);
        if (!Directory.Exists(localFolder))
        {
            _logger.LogInternalDebug("Synthesized knowledge folder does not exist: {Path}", localFolder);
            return 0;
        }

        var uploadedCount = 0;

        foreach (var filePath in Directory.EnumerateFiles(localFolder, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Check if file has changed since last upload
                var lastWriteTime = File.GetLastWriteTimeUtc(filePath);
                if (_memoryFileLastUploaded.TryGetValue(filePath, out var lastUploaded) && lastWriteTime <= lastUploaded)
                {
                    continue;
                }

                var relativePath = filePath.Substring(localFolder.Length).TrimStart(Path.DirectorySeparatorChar);
                var blobPath = $"{SynthesizedKnowledgeFolderName}/{relativePath.Replace(Path.DirectorySeparatorChar, '/')}";
                var content = await File.ReadAllBytesAsync(filePath, cancellationToken);

                await _remoteStorage.UploadAsync(
                    MemoriesContainerName,
                    blobPath,
                    content,
                    cancellationToken);

                _memoryFileLastUploaded[filePath] = lastWriteTime;
                uploadedCount++;
                _logger.LogInternalDebug("Uploaded synthesized knowledge: {BlobPath}", blobPath);
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Failed to upload synthesized knowledge: {FilePath}", filePath);
            }
        }

        if (uploadedCount > 0)
        {
            _logger.LogInternalInformation("Uploaded {Count} synthesized knowledge file(s)", uploadedCount);
        }

        return uploadedCount;
    }

    /// <inheritdoc />
    public async Task<int> DownloadMemoryFilesAsync(CancellationToken cancellationToken = default)
    {
        var downloadedCount = 0;

        try
        {
            // Ensure memories directory exists
            Directory.CreateDirectory(_localSandboxMemoryPath);

            await foreach (var blobPath in _remoteStorage.ListBlobsAsync(MemoriesContainerName, null, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Skip session insights - they are thread-specific and should not be downloaded on startup
                if (blobPath.StartsWith($"{SessionInsightsFolderName}/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var localFilePath = Path.Combine(_localSandboxMemoryPath, blobPath.Replace('/', Path.DirectorySeparatorChar));

                // Ensure directory exists for the file
                var directory = Path.GetDirectoryName(localFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                try
                {
                    var downloaded = await _remoteStorage.DownloadAsync(
                        MemoriesContainerName,
                        blobPath,
                        localFilePath,
                        cancellationToken);

                    if (downloaded)
                    {
                        downloadedCount++;
                        _logger.LogInternalDebug("Downloaded memory file: {BlobPath}", blobPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(ex, "Failed to download memory file: {BlobPath}", blobPath);
                }
            }

            _logger.LogInternalInformation("Downloaded {Count} memory file(s) from remote storage", downloadedCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogInternalError(ex, "Error during memory files download");
        }

        return downloadedCount;
    }

    /// <inheritdoc />
    public async Task<int> DownloadWorkspaceRepoInstructionsAsync(
        string repoName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repoName))
        {
            throw new ArgumentException("Repository name cannot be null or empty.", nameof(repoName));
        }

        var downloadedCount = 0;
        var blobPrefix = $"{RepoInstructionsFolderName}/{repoName}/";

        try
        {
            await foreach (var blobPath in _remoteStorage.ListBlobsAsync(MemoriesContainerName, blobPrefix, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var localFilePath = Path.Combine(_localSandboxMemoryPath, blobPath.Replace('/', Path.DirectorySeparatorChar));

                // Ensure directory exists for the file
                var directory = Path.GetDirectoryName(localFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                try
                {
                    var downloaded = await _remoteStorage.DownloadAsync(
                        MemoriesContainerName,
                        blobPath,
                        localFilePath,
                        cancellationToken);

                    if (downloaded)
                    {
                        downloadedCount++;
                        _logger.LogInternalDebug("Downloaded repo instructions file: {BlobPath}", blobPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(ex, "Failed to download repo instructions file: {BlobPath}", blobPath);
                }
            }

            _logger.LogInternalInformation("Downloaded {Count} repo instructions file(s) for {RepoName}", downloadedCount, repoName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogInternalError(ex, "Error during repo instructions download for {RepoName}", repoName);
        }

        return downloadedCount;
    }

    /// <inheritdoc />
    public async Task<int> DownloadWorkspaceSynthesizedKnowledgeAsync(
        CancellationToken cancellationToken = default)
    {
        var downloadedCount = 0;
        var blobPrefix = $"{SynthesizedKnowledgeFolderName}/";

        try
        {
            await foreach (var blobPath in _remoteStorage.ListBlobsAsync(MemoriesContainerName, blobPrefix, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var localFilePath = Path.Combine(_localSandboxMemoryPath, blobPath.Replace('/', Path.DirectorySeparatorChar));

                // Ensure directory exists for the file
                var directory = Path.GetDirectoryName(localFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                try
                {
                    var downloaded = await _remoteStorage.DownloadAsync(
                        MemoriesContainerName,
                        blobPath,
                        localFilePath,
                        cancellationToken);

                    if (downloaded)
                    {
                        downloadedCount++;
                        _logger.LogInternalDebug("Downloaded synthesized knowledge file: {BlobPath}", blobPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(ex, "Failed to download synthesized knowledge file: {BlobPath}", blobPath);
                }
            }

            _logger.LogInternalInformation("Downloaded {Count} synthesized knowledge file(s)", downloadedCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogInternalError(ex, "Error during synthesized knowledge download");
        }

        return downloadedCount;
    }

    /// <inheritdoc />
    public async Task<MemoryStream?> DownloadWorkspaceSessionInsightsToStreamAsync(
        Guid? threadId,
        CancellationToken cancellationToken = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"session-insights-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempDir);

            var blobPrefix = threadId.HasValue
                ? $"{SessionInsightsFolderName}/{threadId}/"
                : $"{SessionInsightsFolderName}/";

            var downloadedCount = 0;

            await foreach (var blobPath in _remoteStorage.ListBlobsAsync(MemoriesContainerName, blobPrefix, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Get relative path after the prefix
                var relativePath = blobPath.Replace('/', Path.DirectorySeparatorChar);
                var localFilePath = Path.Combine(tempDir, relativePath);

                // Ensure directory exists for the file
                var directory = Path.GetDirectoryName(localFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                try
                {
                    var downloaded = await _remoteStorage.DownloadAsync(
                        MemoriesContainerName,
                        blobPath,
                        localFilePath,
                        cancellationToken);

                    if (downloaded)
                    {
                        downloadedCount++;
                        _logger.LogInternalDebug("Downloaded session insights file to temp: {BlobPath}", blobPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(ex, "Failed to download session insights file: {BlobPath}", blobPath);
                }
            }

            if (downloadedCount == 0)
            {
                _logger.LogInternalInformation("No session insights files found in blob storage for {ThreadId}", threadId?.ToString() ?? "all");
                return null;
            }

            _logger.LogInternalInformation("Downloaded {Count} session insights file(s) to temp directory", downloadedCount);

            // Create tar.gz from temp directory
            var memoryStream = new MemoryStream();
            var sessionInsightsPath = Path.Combine(tempDir, SessionInsightsFolderName);

            if (Directory.Exists(sessionInsightsPath))
            {
                await using (var gzipStream = new System.IO.Compression.GZipStream(memoryStream, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
                await using (var tarWriter = new System.Formats.Tar.TarWriter(gzipStream, leaveOpen: true))
                {
                    await AddDirectoryToTarAsync(tarWriter, sessionInsightsPath, SessionInsightsFolderName, cancellationToken);
                }
            }

            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogInternalError(ex, "Error during session insights stream download for {ThreadId}", threadId?.ToString() ?? "all");
            return null;
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
                _logger.LogInternalWarning(ex, "Failed to clean up temp directory: {TempDir}", tempDir);
            }
        }
    }

    private static async Task AddDirectoryToTarAsync(
        System.Formats.Tar.TarWriter tarWriter,
        string sourceDir,
        string entryBaseName,
        CancellationToken cancellationToken)
    {
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(sourceDir, file);
            var entryName = Path.Combine(entryBaseName, relativePath).Replace(Path.DirectorySeparatorChar, '/');

            var entry = new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.RegularFile, entryName)
            {
                DataStream = File.OpenRead(file)
            };

            await tarWriter.WriteEntryAsync(entry, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<int> DeleteWorkspaceRepoInstructionsAsync(
        string repoName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repoName))
        {
            throw new ArgumentException("Repository name cannot be null or empty.", nameof(repoName));
        }

        var deletedCount = 0;

        // Delete local files
        var localFolder = Path.Combine(_localSandboxMemoryPath, RepoInstructionsFolderName, repoName);
        if (Directory.Exists(localFolder))
        {
            try
            {
                var files = Directory.GetFiles(localFolder, "*", SearchOption.AllDirectories);
                deletedCount += files.Length;

                Directory.Delete(localFolder, recursive: true);
                _logger.LogInternalInformation("Deleted local repo instructions folder: {Path}", localFolder);

                // Clear cache entries for deleted files
                foreach (var file in files)
                {
                    _memoryFileLastUploaded.TryRemove(file, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Failed to delete local repo instructions folder: {Path}", localFolder);
            }
        }

        // Delete from blob storage
        try
        {
            var blobPrefix = $"{RepoInstructionsFolderName}/{repoName}/";
            var blobDeleted = await _remoteStorage.DeleteByPrefixAsync(
                MemoriesContainerName,
                blobPrefix,
                cancellationToken);

            deletedCount += blobDeleted;
            _logger.LogInternalInformation("Deleted {Count} repo instruction file(s) from blob storage for {RepoName}", blobDeleted, repoName);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to delete repo instructions from blob storage for {RepoName}", repoName);
        }

        _logger.LogInternalInformation("Deleted total {Count} repo instruction file(s) for {RepoName}", deletedCount, repoName);
        return deletedCount;
    }

    /// <inheritdoc />
    public async Task<int> DeleteWorkspaceSessionInsightsAsync(
        Guid? threadId,
        CancellationToken cancellationToken = default)
    {
        var deletedCount = 0;

        // Determine which folder(s) to delete
        string localFolder;
        string blobPrefix;

        if (threadId.HasValue)
        {
            localFolder = Path.Combine(_localSandboxMemoryPath, SessionInsightsFolderName, threadId.Value.ToString());
            blobPrefix = $"{SessionInsightsFolderName}/{threadId.Value}/";
        }
        else
        {
            localFolder = Path.Combine(_localSandboxMemoryPath, SessionInsightsFolderName);
            blobPrefix = $"{SessionInsightsFolderName}/";
        }

        // Delete local files
        if (Directory.Exists(localFolder))
        {
            try
            {
                var files = Directory.GetFiles(localFolder, "*", SearchOption.AllDirectories);
                deletedCount += files.Length;

                Directory.Delete(localFolder, recursive: true);
                _logger.LogInternalInformation("Deleted local session insights folder: {Path}", localFolder);

                // Clear cache entries for deleted files
                foreach (var file in files)
                {
                    _memoryFileLastUploaded.TryRemove(file, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Failed to delete local session insights folder: {Path}", localFolder);
            }
        }

        // Delete from blob storage
        try
        {
            var blobDeleted = await _remoteStorage.DeleteByPrefixAsync(
                MemoriesContainerName,
                blobPrefix,
                cancellationToken);

            deletedCount += blobDeleted;
            _logger.LogInternalInformation("Deleted {Count} session insights file(s) from blob storage", blobDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to delete session insights from blob storage");
        }

        _logger.LogInternalInformation("Deleted total {Count} session insights file(s)", deletedCount);
        return deletedCount;
    }

    /// <inheritdoc />
    public async Task<int> DeleteWorkspaceSynthesizedKnowledgeAsync(
        CancellationToken cancellationToken = default)
    {
        var deletedCount = 0;

        // Delete local files
        var localFolder = Path.Combine(_localSandboxMemoryPath, SynthesizedKnowledgeFolderName);
        if (Directory.Exists(localFolder))
        {
            try
            {
                var files = Directory.GetFiles(localFolder, "*", SearchOption.AllDirectories);
                deletedCount += files.Length;

                Directory.Delete(localFolder, recursive: true);
                _logger.LogInternalInformation("Deleted local synthesized knowledge folder: {Path}", localFolder);

                // Clear cache entries for deleted files
                foreach (var file in files)
                {
                    _memoryFileLastUploaded.TryRemove(file, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Failed to delete local synthesized knowledge folder: {Path}", localFolder);
            }
        }

        // Delete from blob storage
        try
        {
            var blobPrefix = $"{SynthesizedKnowledgeFolderName}/";
            var blobDeleted = await _remoteStorage.DeleteByPrefixAsync(
                MemoriesContainerName,
                blobPrefix,
                cancellationToken);

            deletedCount += blobDeleted;
            _logger.LogInternalInformation("Deleted {Count} synthesized knowledge file(s) from blob storage", blobDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to delete synthesized knowledge from blob storage");
        }

        _logger.LogInternalInformation("Deleted total {Count} synthesized knowledge file(s)", deletedCount);
        return deletedCount;
    }

    #endregion
}
