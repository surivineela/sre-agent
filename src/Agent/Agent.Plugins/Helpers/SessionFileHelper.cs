// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Common.ApiModels.Session;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Helpers;

/// <summary>
/// Helper class for handling session files including image results and auto-retrieved files.
/// Provides common functionality used by CodeInterpreterPlugin and PythonFunctionTool.
/// </summary>
public static class SessionFileHelper
{
    /// <summary>
    /// Gets a user-friendly file type category based on extension.
    /// </summary>
    public static string GetFileType(string extension)
    {
        return extension switch
        {
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".webp" or
            ".bmp" or ".tiff" or ".tif" or ".ico" or ".eps" or ".ps" => "Image",

            ".csv" or ".tsv" or ".xlsx" or ".xls" or ".xlsm" or ".ods" or
            ".json" or ".xml" or ".yaml" or ".yml" => "Data",

            ".pdf" or ".html" or ".htm" or ".md" or ".docx" or ".doc" or
            ".pptx" or ".ppt" or ".txt" or ".rtf" => "Document",

            ".py" or ".ipynb" or ".r" or ".sql" or ".sh" => "Code",

            ".zip" or ".tar" or ".gz" or ".h5" or ".hdf5" or ".nc" or
            ".mat" or ".npz" or ".pkl" or ".pickle" => "Archive",

            _ => "File"
        };
    }

    /// <summary>
    /// Processes an image result from code execution by saving it to thread file storage.
    /// </summary>
    /// <param name="imageResult">The image result to process.</param>
    /// <param name="threadId">The thread ID for file storage.</param>
    /// <param name="threadFileStorageService">The file storage service.</param>
    /// <param name="logger">Optional logger for warnings.</param>
    /// <returns>A CodeFileInfo object if the image was saved successfully, null otherwise.</returns>
    public static async Task<CodeFileInfo?> ProcessImageResultAsync(
        ImageExecutionResult imageResult,
        Guid threadId,
        IThreadFileStorageService threadFileStorageService,
        ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(imageResult.Base64Data))
        {
            return null;
        }

        try
        {
            var imageBytes = Convert.FromBase64String(imageResult.Base64Data);
            var imageExtension = imageResult.Format?.ToLowerInvariant() switch
            {
                "png" => ".png",
                "jpg" or "jpeg" => ".jpg",
                "gif" => ".gif",
                "svg" => ".svg",
                "webp" => ".webp",
                _ => ".png" // default to png
            };
            var imageFilename = $"output_{Guid.NewGuid():N}{imageExtension}";

            await SaveToThreadFileStorageAsync(threadId, imageBytes, imageFilename, threadFileStorageService, logger);

            var relativeLink = $"/api/files/{threadId}/{Uri.EscapeDataString(imageFilename)}";
            return new CodeFileInfo
            {
                Filename = imageFilename,
                DownloadLink = relativeLink,
                FileType = "Image"
            };
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to save image result to ThreadFileStorage");
            return null;
        }
    }

    /// <summary>
    /// Retrieves all generated files from a session and saves them to thread file storage.
    /// </summary>
    /// <param name="sessionPoolService">The session pool service.</param>
    /// <param name="sessionIdentifier">The session identifier.</param>
    /// <param name="threadId">The thread ID for file storage.</param>
    /// <param name="threadFileStorageService">The file storage service.</param>
    /// <param name="logger">Optional logger for warnings.</param>
    /// <returns>A list of CodeFileInfo objects for the retrieved files.</returns>
    public static async Task<List<CodeFileInfo>> RetrieveSessionFilesAsync(
        ISessionPoolService sessionPoolService,
        string sessionIdentifier,
        Guid threadId,
        IThreadFileStorageService threadFileStorageService,
        ILogger? logger = null)
    {
        var retrievedFiles = new List<CodeFileInfo>();

        try
        {
            var filesJson = await sessionPoolService.ListSessionFilesAsync(sessionIdentifier);

            // Parse the files list using proper JSON deserialization
            if (!string.IsNullOrWhiteSpace(filesJson))
            {
                var filesResponse = JsonSerializer.Deserialize<FilesListResponse>(filesJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (filesResponse?.Value != null && filesResponse.Value.Count > 0)
                {
                    foreach (var fileWrapper in filesResponse.Value)
                    {
                        if (fileWrapper.Properties == null)
                        {
                            continue;
                        }

                        var file = fileWrapper.Properties;

                        // Skip if filename is empty or if it's a directory indicator
                        if (string.IsNullOrWhiteSpace(file.Filename) || file.Filename.EndsWith('/'))
                        {
                            continue;
                        }

                        try
                        {
                            var fileBytes = await sessionPoolService.DownloadSessionFileAsync(sessionIdentifier, file.Filename);
                            var extension = Path.GetExtension(file.Filename).ToLowerInvariant();
                            var fileType = GetFileType(extension);

                            // Store to ThreadFileStorage for persistence
                            await SaveToThreadFileStorageAsync(threadId, fileBytes, file.Filename, threadFileStorageService, logger);

                            var relativeLink = $"/api/files/{threadId}/{Uri.EscapeDataString(Path.GetFileName(file.Filename))}";

                            retrievedFiles.Add(new CodeFileInfo
                            {
                                Filename = Path.GetFileName(file.Filename),
                                DownloadLink = relativeLink,
                                FileType = fileType
                            });
                        }
                        catch (Exception ex)
                        {
                            logger?.LogWarning("Failed to auto-retrieve file '{Filename}': {Message}", file.Filename, ex.Message);
                            // Don't fail the whole operation if one file fails
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to auto-retrieve session files");
            // Don't fail the whole operation if file retrieval fails
        }

        return retrievedFiles;
    }

    /// <summary>
    /// Saves file bytes to ThreadFileStorage for persistence across sessions.
    /// </summary>
    public static async Task SaveToThreadFileStorageAsync(
        Guid threadId,
        byte[] fileBytes,
        string filename,
        IThreadFileStorageService threadFileStorageService,
        ILogger? logger = null)
    {
        try
        {
            var fileKey = await threadFileStorageService.UploadThreadFileAsync(
                threadId,
                filename,
                fileBytes);
            logger?.LogInformation("Saved file to ThreadFileStorage: {Filename} -> {FileKey}", filename, fileKey);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to save file to ThreadFileStorage: {Filename}", filename);
            // Don't fail the operation if ThreadFileStorage save fails
        }
    }

    /// <summary>
    /// Processes all files from a code execution response, handling both image results and session files.
    /// This is the main entry point that combines image and session file processing.
    /// </summary>
    /// <param name="execResp">The code execution response to process.</param>
    /// <param name="sessionPoolService">The session pool service.</param>
    /// <param name="sessionIdentifier">The session identifier.</param>
    /// <param name="threadId">The thread ID for file storage.</param>
    /// <param name="threadFileStorageService">The file storage service.</param>
    /// <param name="logger">Optional logger for warnings.</param>
    public static async Task ProcessExecutionFilesAsync(
        CodeExecutionResponse execResp,
        ISessionPoolService sessionPoolService,
        string sessionIdentifier,
        Guid threadId,
        IThreadFileStorageService threadFileStorageService,
        ILogger? logger = null)
    {
        // Process image result if present
        if (execResp.Result is ImageExecutionResult imageResult && !string.IsNullOrEmpty(imageResult.Base64Data))
        {
            var imageFile = await ProcessImageResultAsync(imageResult, threadId, threadFileStorageService, logger);
            if (imageFile != null)
            {
                execResp.ImageFile = imageFile;
            }
        }

        // Auto-retrieve all session files
        var retrievedFiles = await RetrieveSessionFilesAsync(
            sessionPoolService,
            sessionIdentifier,
            threadId,
            threadFileStorageService,
            logger);

        if (retrievedFiles.Count > 0)
        {
            execResp.RetrievedFiles = retrievedFiles;
        }
    }

    // Internal classes for JSON deserialization (same as CodeInterpreterPlugin)
    private class FilesListResponse
    {
        public List<FileItemWrapper>? Value { get; set; }
    }

    private class FileItemWrapper
    {
        public FileMetadata? Properties { get; set; }
    }

    private class FileMetadata
    {
        public string Filename { get; set; } = string.Empty;
        public long? Size { get; set; }
        public DateTime? LastModifiedTime { get; set; }
    }
}
