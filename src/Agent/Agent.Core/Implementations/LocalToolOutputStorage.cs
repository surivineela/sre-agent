// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        _baseStoragePath = baseStoragePath;
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

    /// <summary>
    /// Verifies that a file exists in storage and returns its local file path.
    /// </summary>
    public string? EnsureFileExist(string fileId)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return null;
        }

        try
        {
            // Sanitize fileId to prevent path traversal and glob pattern injection
            var sanitizedFileId = Path.GetFileName(fileId);

            if (string.IsNullOrWhiteSpace(sanitizedFileId) ||
                sanitizedFileId.Contains("..") ||
                sanitizedFileId.IndexOfAny(new[] { '*', '?' }) >= 0)
            {
                _logger.LogInternalWarning("Invalid fileId detected: {FileId}", fileId);
                return null;
            }

            if (!Directory.Exists(_baseStoragePath))
            {
                return null;
            }

            // Search in base directory and all subdirectories
            var files = Directory.EnumerateFiles(_baseStoragePath, sanitizedFileId, SearchOption.AllDirectories);
            var filePath = files.FirstOrDefault();

            if (filePath == null)
            {
                _logger.LogInternalWarning("File not found for {FileId}", fileId);
                return null;
            }

            // Verify the resolved path is within base storage path
            var fullPath = Path.GetFullPath(filePath);
            var basePath = Path.GetFullPath(_baseStoragePath);

            if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInternalWarning("Path traversal attempt detected for {FileId}", fileId);
                return null;
            }

            _logger.LogInternalInformation("File exists for {FileId}: {FilePath}", fileId, filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error checking if file exists: {FileId}", fileId);
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
            var files = Directory.EnumerateFiles(_baseStoragePath, "*.txt", SearchOption.AllDirectories);
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

