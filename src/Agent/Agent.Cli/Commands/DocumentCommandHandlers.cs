// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

/// <summary>
/// Command handlers for document-related operations.
/// </summary>
public static class DocumentCommandHandlers
{
    /// <summary>
    /// Handles the document upload command.
    /// </summary>
    /// <param name="parseResult">Command line parse result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task<int> HandleUploadCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting document upload command");

        // Extract options
        var filePaths = parseResult.GetValue(DocumentCommandOptions.Upload.FileOption);
        var folderPath = parseResult.GetValue(DocumentCommandOptions.Upload.FolderOption);
        var triggerIndexing = parseResult.GetValue(DocumentCommandOptions.Upload.TriggerIndexingOption);
        var noIndexing = parseResult.GetValue(DocumentCommandOptions.Upload.NoIndexingOption);
        var recursive = parseResult.GetValue(DocumentCommandOptions.Upload.RecursiveOption);

        DebugLogger.Debug("Parameters", $"Files: {(filePaths?.Length > 0 ? string.Join(", ", filePaths) : "none")}, Folder: {folderPath ?? "none"}, TriggerIndexing: {triggerIndexing}, NoIndexing: {noIndexing}, Recursive: {recursive}");

        // Show deprecation warnings
        if (!string.IsNullOrEmpty(folderPath))
        {
            ConsoleUI.WriteInfo("⚠️  Warning: '--folder' is deprecated and will be removed in a future release.", ConsoleColor.Yellow);
            ConsoleUI.WriteInfo("    Please use '--file' instead, which supports both files and folders.", ConsoleColor.Yellow);
            Console.WriteLine();
        }

        if (triggerIndexing)
        {
            ConsoleUI.WriteInfo("⚠️  Warning: '--trigger-indexing' is deprecated and will be removed in a future release.", ConsoleColor.Yellow);
            ConsoleUI.WriteInfo("    Indexing is now triggered by default. Use '--no-indexing' to skip indexing.", ConsoleColor.Yellow);
            Console.WriteLine();
        }

        if (recursive)
        {
            ConsoleUI.WriteInfo("⚠️  Warning: '--recursive' is deprecated and will be removed in a future release.", ConsoleColor.Yellow);
            ConsoleUI.WriteInfo("    Folders are now always searched recursively.", ConsoleColor.Yellow);
            Console.WriteLine();
        }

        // Gather all input paths
        var inputPaths = new List<string>();
        if (filePaths != null && filePaths.Length > 0)
        {
            inputPaths.AddRange(filePaths);
        }
        if (!string.IsNullOrEmpty(folderPath))
        {
            inputPaths.Add(folderPath);
        }

        // Determine indexing setting (default to true unless --no-indexing is specified)
        var shouldTriggerIndexing = !noIndexing;

        // Gather all files from input paths
        var filesToUpload = await GatherFilesFromPaths(inputPaths);

        if (filesToUpload.Count == 0)
        {
            ConsoleUI.WriteStatus(false, "No valid files found to upload. Supported file types: .md, .txt");
            return 1;
        }

        // Show what will be uploaded
        if (filesToUpload.Count == 1)
        {
            ConsoleUI.WriteInfo($"Uploading file: {Path.GetFileName(filesToUpload[0])}", ConsoleColor.Cyan);
        }
        else
        {
            ConsoleUI.WriteInfo($"Uploading {filesToUpload.Count} files", ConsoleColor.Cyan);
        }

        // Validate all files before upload
        var validationErrors = ValidateFiles(filesToUpload);
        if (validationErrors.Count > 0)
        {
            ConsoleUI.WriteStatus(false, "Validation failed:");
            foreach (var error in validationErrors)
            {
                ConsoleUI.WriteBullet(error, ConsoleColor.Red, 3);
            }
            return 1;
        }

        // Show file list for multiple files
        if (filesToUpload.Count > 1)
        {
            foreach (var file in filesToUpload)
            {
                ConsoleUI.WriteBullet(Path.GetFileName(file), ConsoleColor.Gray, 3);
                DebugLogger.LogFile("UPLOAD", file, $"Size: {new FileInfo(file).Length} bytes");
            }
        }

        // Perform the upload
        using var apiService = new ApiService();
        var (Success, Response) = await apiService.UploadDocumentsAsync(filesToUpload, shouldTriggerIndexing);
        var success = Success;
        var response = Response;

        if (success)
        {
            ConsoleUI.WriteStatus(true, response);
            return 0;
        }
        else
        {
            ConsoleUI.WriteStatus(false, response);
            return 1;
        }
    }

    /// <summary>
    /// Gathers all files from the provided paths (files and/or directories).
    /// Directories are searched recursively for supported file types.
    /// </summary>
    /// <param name="paths">List of file or directory paths</param>
    /// <returns>List of file paths to upload</returns>
    private static Task<List<string>> GatherFilesFromPaths(List<string> paths)
    {
        var files = new List<string>();
        var allowedExtensions = new[] { ".md", ".txt" };

        foreach (var path in paths)
        {
            try
            {
                var absolutePath = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);

                if (File.Exists(absolutePath))
                {
                    // It's a file - add it regardless of extension, but warn if not supported
                    var extension = Path.GetExtension(absolutePath).ToLowerInvariant();
                    if (!allowedExtensions.Contains(extension))
                    {
                        ConsoleUI.WriteInfo($"Warning: File type '{extension}' may not be supported. Recommended types: .md, .txt", ConsoleColor.Yellow);
                    }
                    files.Add(absolutePath);
                }
                else if (Directory.Exists(absolutePath))
                {
                    // It's a directory - search recursively for supported files
                    foreach (var extension in allowedExtensions)
                    {
                        var pattern = $"*{extension}";
                        var foundFiles = Directory.GetFiles(absolutePath, pattern, SearchOption.AllDirectories);
                        files.AddRange(foundFiles);
                    }
                }
                else
                {
                    ConsoleUI.WriteInfo($"Path not found: {path}", ConsoleColor.Yellow);
                }
            }
            catch (Exception ex)
            {
                ConsoleUI.WriteInfo($"Error accessing path '{path}': {ex.Message}", ConsoleColor.Yellow);
            }
        }

        // Remove duplicates - use case-insensitive comparison on Windows, case-sensitive on Unix
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var uniqueFiles = files.Distinct(comparer).ToList();

        return Task.FromResult(uniqueFiles);
    }

    /// <summary>
    /// Validates a list of files for upload.
    /// Checks that files exist, are not empty, don't exceed size limits, and have valid extensions.
    /// </summary>
    /// <param name="files">List of file paths to validate</param>
    /// <returns>List of validation error messages</returns>
    private static List<string> ValidateFiles(List<string> files)
    {
        var errors = new List<string>();
        var allowedExtensions = new[] { ".md", ".txt" };
        const long maxFileSize = 16 * 1024 * 1024; // 16MB

        foreach (var file in files)
        {
            try
            {
                if (!File.Exists(file))
                {
                    errors.Add($"File not found: {file}");
                    continue;
                }

                var fileInfo = new FileInfo(file);
                var extension = fileInfo.Extension.ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    errors.Add($"Unsupported file type: {Path.GetFileName(file)} ({extension}). Supported types: .md, .txt");
                    continue;
                }

                if (fileInfo.Length == 0)
                {
                    errors.Add($"File is empty: {Path.GetFileName(file)}");
                    continue;
                }

                if (fileInfo.Length > maxFileSize)
                {
                    errors.Add($"File exceeds 16MB limit: {Path.GetFileName(file)} ({fileInfo.Length:N0} bytes)");
                    continue;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error validating file '{Path.GetFileName(file)}': {ex.Message}");
            }
        }

        return errors;
    }

    /// <summary>
    /// Handles the document search command.
    /// </summary>
    /// <param name="parseResult">Command line parse result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task<int> HandleSearchCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting document search command");

        // Extract options
        var query = parseResult.GetValue(DocumentCommandOptions.Search.QueryOption);
        DebugLogger.Debug("Parameters", $"Query: {query}");

        // Validate query parameter
        if (string.IsNullOrWhiteSpace(query))
        {
            ConsoleUI.WriteStatus(false, "Error: --query parameter is required for document search.");
            return 1;
        }

        ConsoleUI.WriteInfo($"Searching for documents related to: \"{query}\"", ConsoleColor.Cyan);
        Console.WriteLine();

        // Perform the search
        using var apiService = new ApiService();
        var (results, message) = await apiService.SearchDocumentsAsync(query);

        if (results.Count == 0)
        {
            ConsoleUI.WriteStatus(false, message);
            Console.WriteLine();
            ConsoleUI.WriteInfo("Try:", ConsoleColor.Yellow);
            ConsoleUI.WriteBullet("Using different keywords", ConsoleColor.Gray);
            ConsoleUI.WriteBullet("Making your query more general", ConsoleColor.Gray);
            ConsoleUI.WriteBullet("Checking if documents have been uploaded and indexed", ConsoleColor.Gray);
            return 1;
        }

        ConsoleUI.WriteSection("Search Results");
        for (int i = 0; i < results.Count; i++)
        {
            var content = results[i];
            Console.WriteLine();
            ConsoleUI.WriteBullet($"Result {i + 1}", ConsoleColor.White, 0);

            // Truncate content if too long for display
            var displayContent = content.Length > 500 ? content.Substring(0, 500) + "..." : content;
            Console.WriteLine();
            Console.WriteLine(displayContent);
        }

        Console.WriteLine();
        ConsoleUI.WriteStatus(true, message);
        return 0;
    }

    /// <summary>
    /// Handles the document delete command.
    /// </summary>
    /// <param name="parseResult">Command line parse result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task<int> HandleDeleteCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting document delete command");

        // Extract options
        var name = parseResult.GetValue(DocumentCommandOptions.Delete.NameOption);

        DebugLogger.Debug("Parameters", $"Name: {name}");

        ConsoleUI.WriteInfo($"Deleting document: {name}", ConsoleColor.Cyan);
        Console.WriteLine();

        // Perform the deletion
        using var apiService = new ApiService();
        var (success, response) = await apiService.DeleteDocumentAsync(name!);

        if (success)
        {
            ConsoleUI.WriteStatus(true, response);
            return 0;
        }
        else
        {
            ConsoleUI.WriteStatus(false, response);
            return 1;
        }
    }

    /// <summary>
    /// Handles the document get command.
    /// </summary>
    /// <param name="parseResult">Command line parse result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task<int> HandleGetCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting document get command");

        // Extract options
        var prefix = parseResult.GetValue(DocumentCommandOptions.Get.PrefixOption);

        DebugLogger.Debug("Parameters", $"Prefix: {prefix ?? "none"}");

        // Perform the listing
        using var apiService = new ApiService();
        var (files, error) = await apiService.ListDocumentsAsync(prefix);

        if (error != null)
        {
            ConsoleUI.WriteStatus(false, error);
            return 1;
        }

        if (files.Count == 0)
        {
            ConsoleUI.WriteInfo("No documents found.", ConsoleColor.Yellow);
            return 0;
        }

        ConsoleUI.WriteSection("Uploaded Documents");

        foreach (var file in files)
        {
            ConsoleUI.WriteBullet(file);
        }

        Console.WriteLine();
        ConsoleUI.WriteKeyValue("Total", $"{files.Count} document(s)", 0);

        return 0;
    }

    /// <summary>
    /// Handles the document reindex command.
    /// </summary>
    /// <param name="parseResult">Command line parse result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task<int> HandleReindexCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting document reindex command");

        ConsoleUI.WriteInfo("Triggering document reindexing...", ConsoleColor.Cyan);
        Console.WriteLine();

        // Perform the reindexing
        using var apiService = new ApiService();
        var (success, message) = await apiService.ReindexDocumentsAsync();

        if (success)
        {
            ConsoleUI.WriteStatus(true, message);
            Console.WriteLine();
            ConsoleUI.WriteInfo("All documents in the knowledge base will be reprocessed and reindexed.", ConsoleColor.Gray);
            ConsoleUI.WriteInfo("This may take a few minutes depending on the number of documents.", ConsoleColor.Gray);
            return 0;
        }
        else
        {
            ConsoleUI.WriteStatus(false, message);
            return 1;
        }
    }
}
