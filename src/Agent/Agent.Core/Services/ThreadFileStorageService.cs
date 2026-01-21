// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Core.Services;

/// <summary>
/// Service for managing thread files and tool outputs with local caching.
/// Uses NoOpRemoteFileStorage when remote storage is not configured, so all remote operations are safe to call.
/// </summary>
public class ThreadFileStorageService : IThreadFileStorageService
{
    private readonly IRemoteFileStorage _remoteStorage;
    private readonly ToolOutputSettings _settings;
    private readonly ILogger<ThreadFileStorageService> _logger;
    private readonly string _localToolOutputPath;
    private readonly string _localThreadFilesPath;

    public ThreadFileStorageService(
        IOptions<ToolOutputSettings> settings,
        ILogger<ThreadFileStorageService> logger,
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
}
