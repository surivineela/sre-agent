using System.CommandLine;
using System.CommandLine.Parsing;
using Agent.Cli.Services;
using Agent.Cli.Helpers;

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
    public static async Task HandleUploadCommand(ParseResult parseResult)
    {
        // Set debug mode first
        var debug = parseResult.GetValue(DocumentCommandOptions.DebugOption);
        DebugLogger.SetDebugMode(debug);

        DebugLogger.Debug("Command", "Starting document upload command");

        try
        {
            // Extract options
            var filePath = parseResult.GetValue(DocumentCommandOptions.FileOption);
            var folderPath = parseResult.GetValue(DocumentCommandOptions.FolderOption);
            var triggerIndexing = parseResult.GetValue(DocumentCommandOptions.TriggerIndexingOption);
            var noIndexing = parseResult.GetValue(DocumentCommandOptions.NoIndexingOption);
            var recursive = parseResult.GetValue(DocumentCommandOptions.RecursiveOption);

            DebugLogger.Debug("Parameters", $"File: {filePath ?? "none"}, Folder: {folderPath ?? "none"}, TriggerIndexing: {triggerIndexing}, NoIndexing: {noIndexing}, Recursive: {recursive}");

            // Validate mutually exclusive options
            if (!string.IsNullOrEmpty(filePath) && !string.IsNullOrEmpty(folderPath))
            {
                Console.WriteLine("❌ Error: --file and --folder options are mutually exclusive. Use only one.");
                Environment.Exit(1);
                return;
            }

            if (string.IsNullOrEmpty(filePath) && string.IsNullOrEmpty(folderPath))
            {
                Console.WriteLine("❌ Error: Either --file or --folder must be specified.");
                Environment.Exit(1);
                return;
            }

            // Determine indexing setting (default to true unless --no-indexing is specified)
            var shouldTriggerIndexing = !noIndexing;

            // Set default recursive behavior (true unless explicitly set to false)
            var shouldSearchRecursive = recursive;

            List<string> filesToUpload;

            if (!string.IsNullOrEmpty(filePath))
            {
                // Single file upload
                DebugLogger.Debug("FileProcessing", $"Processing single file: {filePath}");
                filesToUpload = await GetSingleFileForUpload(filePath);
            }
            else
            {
                // Folder upload
                DebugLogger.Debug("FileProcessing", $"Processing folder: {folderPath}, recursive: {shouldSearchRecursive}");
                filesToUpload = await GetFilesFromFolder(folderPath!, shouldSearchRecursive);
            }

            if (filesToUpload.Count == 0)
            {
                Console.WriteLine("❌ No valid files found to upload. Supported file types: .md, .txt");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"📁 Found {filesToUpload.Count} file(s) to upload");
            foreach (var file in filesToUpload)
            {
                Console.WriteLine($"   📄 {Path.GetFileName(file)}");
                DebugLogger.LogFile("UPLOAD", file, $"Size: {new FileInfo(file).Length} bytes");
            }

            // Perform the upload
            using var apiService = new ApiService();
            var result = await apiService.UploadDocumentsAsync(filesToUpload, shouldTriggerIndexing);
            var success = result.Item1;
            var response = result.Item2;

            if (success)
            {
                Console.WriteLine($"✅ {response}");
            }
            else
            {
                Console.WriteLine($"❌ {response}");
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"DocumentUpload failed: {ex.Message}");
            Console.WriteLine($"❌ Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Gets a single file for upload, validating its existence and type.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>List containing the validated file path</returns>
    private static Task<List<string>> GetSingleFileForUpload(string filePath)
    {
        var files = new List<string>();

        try
        {
            // Convert relative path to absolute if needed
            var absolutePath = Path.IsPathRooted(filePath) ? filePath : Path.GetFullPath(filePath);

            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            // Validate file extension (matching AgentMemoryController allowedExtensions)
            var extension = Path.GetExtension(absolutePath).ToLowerInvariant();
            var allowedExtensions = new[] { ".md", ".txt" };

            if (!allowedExtensions.Contains(extension))
            {
                throw new ArgumentException($"Unsupported file type: {extension}. Supported types: {string.Join(", ", allowedExtensions)}");
            }

            // Validate file size (16MB limit as per AgentMemoryController)
            var fileInfo = new FileInfo(absolutePath);
            const long maxFileSize = 16 * 1024 * 1024; // 16MB
            if (fileInfo.Length > maxFileSize)
            {
                throw new ArgumentException($"File size ({fileInfo.Length} bytes) exceeds maximum limit of 16MB");
            }

            if (fileInfo.Length == 0)
            {
                throw new ArgumentException("File is empty");
            }

            files.Add(absolutePath);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid file '{filePath}': {ex.Message}", ex);
        }

        return Task.FromResult(files);
    }

    /// <summary>
    /// Gets all supported files from a folder.
    /// </summary>
    /// <param name="folderPath">Path to the folder</param>
    /// <param name="recursive">Whether to search recursively</param>
    /// <returns>List of file paths to upload</returns>
    private static Task<List<string>> GetFilesFromFolder(string folderPath, bool recursive)
    {
        var files = new List<string>();

        try
        {
            // Convert relative path to absolute if needed
            var absolutePath = Path.IsPathRooted(folderPath) ? folderPath : Path.GetFullPath(folderPath);

            if (!Directory.Exists(absolutePath))
            {
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
            }

            // Supported extensions (matching AgentMemoryController)
            var allowedExtensions = new[] { ".md", ".txt" };
            const long maxFileSize = 16 * 1024 * 1024; // 16MB

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            foreach (var extension in allowedExtensions)
            {
                var pattern = $"*{extension}";
                var foundFiles = Directory.GetFiles(absolutePath, pattern, searchOption);

                foreach (var file in foundFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);

                        // Skip empty files
                        if (fileInfo.Length == 0)
                        {
                            Console.WriteLine($"⚠️  Skipping empty file: {Path.GetFileName(file)}");
                            continue;
                        }

                        // Skip files that are too large
                        if (fileInfo.Length > maxFileSize)
                        {
                            Console.WriteLine($"⚠️  Skipping file exceeding 16MB limit: {Path.GetFileName(file)} ({fileInfo.Length} bytes)");
                            continue;
                        }

                        files.Add(file);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️  Skipping file due to error: {Path.GetFileName(file)} - {ex.Message}");
                    }
                }
            }

            if (files.Count == 0)
            {
                var searchDescription = recursive ? "recursively" : "in top-level directory";
                throw new ArgumentException($"No valid .md or .txt files found in '{folderPath}' {searchDescription}");
            }
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            throw new ArgumentException($"Error accessing folder '{folderPath}': {ex.Message}", ex);
        }

        return Task.FromResult(files);
    }

    /// <summary>
    /// Handles the document search command.
    /// </summary>
    /// <param name="parseResult">Command line parse result</param>
    public static async Task HandleSearchCommand(ParseResult parseResult)
    {
        // Set debug mode first
        var debug = parseResult.GetValue(DocumentCommandOptions.DebugOption);
        DebugLogger.SetDebugMode(debug);

        DebugLogger.Debug("Command", "Starting document search command");

        try
        {
            // Extract options
            var query = parseResult.GetValue(DocumentCommandOptions.QueryOption);

            DebugLogger.Debug("Parameters", $"Query: {query}");

            // Validate query parameter
            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine("❌ Error: --query parameter is required for document search.");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"🔍 Searching for documents related to: \"{query}\"");
            Console.WriteLine();

            // Perform the search
            using var apiService = new ApiService();
            var result = await apiService.SearchDocumentsAsync(query);
            var success = result.Item1;
            var response = result.Item2;

            if (success)
            {
                Console.WriteLine($"✅ {response}");
            }
            else
            {
                Console.WriteLine($"❌ {response}");
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"DocumentSearch failed: {ex.Message}");
            Console.WriteLine($"❌ Search failed: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the document reindex command.
    /// </summary>
    /// <param name="parseResult">Command line parse result</param>
    public static async Task HandleReindexCommand(ParseResult parseResult)
    {
        // Set debug mode first
        var debug = parseResult.GetValue(DocumentCommandOptions.DebugOption);
        DebugLogger.SetDebugMode(debug);

        DebugLogger.Debug("Command", "Starting document reindex command");

        try
        {
            Console.WriteLine("🔄 Triggering document reindexing...");
            Console.WriteLine();

            // Perform the reindexing
            using var apiService = new ApiService();
            var result = await apiService.ReindexDocumentsAsync();
            var success = result.Item1;
            var response = result.Item2;

            if (success)
            {
                Console.WriteLine(response);
                Console.WriteLine();
                Console.WriteLine("📚 All documents in the knowledge base will be reprocessed and reindexed.");
                Console.WriteLine("   This may take a few minutes depending on the number of documents.");
            }
            else
            {
                Console.WriteLine(response);
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"DocumentReindex failed: {ex.Message}");
            Console.WriteLine($"❌ Reindexing failed: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
