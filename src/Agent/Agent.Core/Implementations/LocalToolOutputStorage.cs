// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Interfaces;
using Agent.Logging;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Implementations;

/// <summary>
/// Local file system implementation of tool output storage.
/// Organizes outputs by thread ID and tool name for easy navigation and cleanup.
///
/// </summary>
public class LocalToolOutputStorage : IToolOutputStorage
{
    private readonly string _baseStoragePath;
    private readonly ILogger<LocalToolOutputStorage> _logger;

    public LocalToolOutputStorage(
        string baseStoragePath,
        ILogger<LocalToolOutputStorage> logger)
    {
        if (string.IsNullOrWhiteSpace(baseStoragePath))
        {
            throw new ArgumentException("Base storage path cannot be null or empty.", nameof(baseStoragePath));
        }

        // Always append "ToolOutput" subfolder to the base path
        _baseStoragePath = Path.Combine(baseStoragePath, "ToolOutput");
        _logger = logger;

        // Ensure base directory exists
        try
        {
            Directory.CreateDirectory(_baseStoragePath);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to create base storage directory at {BasePath}", _baseStoragePath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> SaveAsync(
        Guid threadId,
        string toolName,
        string content,
        string fileExtension,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be null or empty.", nameof(content));
        }

        // Add dot prefix to file extension if not present
        if (!fileExtension.StartsWith("."))
        {
            fileExtension = $".{fileExtension}";
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var fileKey = $"{threadId}-{timestamp}{fileExtension}";
        var lineCount = content.Split('\n').Length;
        var contentLength = content.Length;

        _logger.LogInternalInformation(
            "Saving tool output for thread {ThreadId}, tool {ToolName}, fileKey {FileKey}, lines {LineCount}, length {Length}, extension {Extension}",
            threadId, toolName, fileKey, lineCount, contentLength, fileExtension);

        try
        {
            // Save content to file in base storage directory
            var filePath = Path.Combine(_baseStoragePath, fileKey);
            await File.WriteAllTextAsync(filePath, content, cancellationToken);

            _logger.LogInternalInformation(
                "Successfully saved tool output to {FilePath}",
                filePath);

            return fileKey;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to save tool output for thread {ThreadId}, tool {ToolName}", threadId, toolName);
            throw;
        }
    }

    /// <summary>
    /// Verifies that a file exists in storage and returns its local file path.
    /// </summary>
    public string? EnsureFileExist(string fileKey)
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

            if (!Directory.Exists(_baseStoragePath))
            {
                return null;
            }

            // Search in base directory
            var filePath = Path.Combine(_baseStoragePath, sanitizedFileKey);

            if (!File.Exists(filePath))
            {
                _logger.LogInternalWarning("File not found for {FileKey}", fileKey);
                return null;
            }

            // Verify the resolved path is within base storage path
            var fullPath = Path.GetFullPath(filePath);
            var basePath = Path.GetFullPath(_baseStoragePath);

            if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInternalWarning("Path traversal attempt detected for {FileKey}", fileKey);
                return null;
            }

            _logger.LogInternalInformation("File exists for {FileKey}: {FilePath}", fileKey, filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error checking if file exists: {FileKey}", fileKey);
            return null;
        }
    }

    /// <summary>
    /// Deletes files older than the specified retention period
    /// </summary>
    /// <param name="retentionDays">Number of days to retain files</param>
    /// <returns>Number of files deleted</returns>
    public int CleanupOldFiles(int retentionDays)
    {
        if (retentionDays <= 0)
        {
            _logger.LogInternalWarning("Invalid retention days: {RetentionDays}. Skipping cleanup.", retentionDays);
            return 0;
        }

        try
        {
            if (!Directory.Exists(_baseStoragePath))
            {
                _logger.LogInternalInformation("Base storage path does not exist. Nothing to cleanup.");
                return 0;
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var files = Directory.EnumerateFiles(_baseStoragePath, "*.*", SearchOption.AllDirectories);
            var deletedCount = 0;

            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTimeUtc < cutoffDate)
                    {
                        File.Delete(file);
                        deletedCount++;
                        _logger.LogInternalInformation("Deleted old file: {FilePath}, Created: {CreationTime}",
                            file, fileInfo.CreationTimeUtc);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to delete file: {FilePath}", file);
                }
            }

            _logger.LogInternalInformation("Cleanup completed. Deleted {DeletedCount} files older than {RetentionDays} days.",
                deletedCount, retentionDays);
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error during cleanup of old files in {BasePath}", _baseStoragePath);
            return 0;
        }
    }
}

