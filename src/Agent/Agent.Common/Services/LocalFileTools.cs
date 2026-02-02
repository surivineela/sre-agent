// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Common.ApiModels;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Agent.Common.Services;

/// <summary>
/// Local file system implementation of IFileTool.
/// Provides file operations sandboxed to a specific root directory.
/// </summary>
public class LocalFileTools : IFileTool
{
    private readonly ILogger _logger;
    private readonly string _sandboxRoot;

    /// <summary>
    /// Maximum lines to read in a single operation.
    /// </summary>
    public const int MaxLinesPerRead = 2000;

    /// <summary>
    /// Default maximum search results to return.
    /// </summary>
    public const int DefaultMaxSearchResults = 20;

    /// <summary>
    /// Default folders to exclude from search (matches VS Code search.exclude defaults).
    /// </summary>
    public static readonly HashSet<string> DefaultExcludedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".svn", ".hg", ".bzr",
        "node_modules", "bower_components",
        "__pycache__", ".pytest_cache", ".mypy_cache",
        "venv", ".venv", "env", ".env",
        "bin", "obj", "out", "dist", "build", "target",
        ".vs", ".vscode", ".idea",
        "coverage", ".nyc_output",
        ".next", ".nuxt", ".cache",
        "vendor"
    };

    /// <summary>
    /// Binary file extensions to skip during text search.
    /// </summary>
    public static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Compiled/Binary
        ".exe", ".dll", ".so", ".dylib", ".a", ".o", ".obj", ".lib", ".pdb",
        ".class", ".jar", ".war", ".ear", ".pyc", ".pyo", ".wasm",
        // Archives
        ".zip", ".tar", ".gz", ".bz2", ".xz", ".7z", ".rar", ".iso",
        // Images
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg", ".webp", ".tiff",
        // Documents
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        // Media
        ".mp3", ".mp4", ".avi", ".mov", ".mkv", ".wav", ".flac", ".ogg",
        // Fonts
        ".ttf", ".otf", ".woff", ".woff2", ".eot",
        // Data
        ".sqlite", ".db", ".mdb", ".parquet",
        // Package locks (large and not useful for search)
        ".lock"
    };

    /// <summary>
    /// Initializes a new instance of the LocalFileTools class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="sandboxRoot">The root directory for all file operations.</param>
    public LocalFileTools(ILogger logger, string sandboxRoot)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sandboxRoot = sandboxRoot ?? throw new ArgumentNullException(nameof(sandboxRoot));

        if (!Directory.Exists(_sandboxRoot))
        {
            Directory.CreateDirectory(_sandboxRoot);
        }
    }

    #region Path Validation

    /// <summary>
    /// Validates that the given path is within the sandbox root.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <returns>The validated full path.</returns>
    /// <exception cref="ArgumentException">If the path is empty.</exception>
    /// <exception cref="UnauthorizedAccessException">If the path is outside the sandbox root.</exception>
    public string ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty");
        }

        // Convert relative paths to absolute paths relative to SandboxRoot
        string fullPath;
        if (!Path.IsPathRooted(path))
        {
            fullPath = Path.GetFullPath(Path.Combine(_sandboxRoot, path));
        }
        else
        {
            fullPath = Path.GetFullPath(path);
        }

        if (!fullPath.StartsWith(_sandboxRoot, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"Path must be within sandbox root: {_sandboxRoot}");
        }

        return fullPath;
    }

    #endregion

    #region File Operations

    /// <summary>
    /// Reads a file from the local file system.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <param name="startLine">1-based starting line number.</param>
    /// <param name="endLine">1-based ending line number.</param>
    /// <returns>Formatted string with file content.</returns>
    public async Task<string> ReadFileAsync(string filePath, int startLine, int endLine)
    {
        try
        {
            var validPath = ValidatePath(filePath);

            if (!File.Exists(validPath))
            {
                return $"Tool call failed: File does not exist: {filePath}";
            }

            // Swap if start > end (forgiving behavior)
            if (startLine > endLine)
            {
                (startLine, endLine) = (endLine, startLine);
            }

            var fileContent = await File.ReadAllTextAsync(validPath);
            var normalizedContent = NormalizeLineEndings(fileContent);
            var allLines = normalizedContent.Split('\n');
            var totalLines = allLines.Length;

            if (totalLines == 0)
            {
                return $"File: `{filePath}` (empty file)";
            }

            // Convert to 0-indexed and clamp to bounds
            var start = Math.Max(0, startLine - 1);
            var end = Math.Min(totalLines - 1, endLine - 1);

            if (start >= totalLines)
            {
                return $"Tool call failed: Start line {startLine} is beyond file length ({totalLines} lines)";
            }

            var linesToRead = end - start + 1;
            var truncated = false;

            if (linesToRead > MaxLinesPerRead)
            {
                end = start + MaxLinesPerRead - 1;
                linesToRead = MaxLinesPerRead;
                truncated = true;
            }

            var selectedLines = allLines.Skip(start).Take(linesToRead);
            var content = string.Join("\n", selectedLines);

            var sb = new StringBuilder();
            sb.AppendLine($"File: `{filePath}`. Lines {start + 1} to {end + 1} ({totalLines} lines total):");
            sb.Append(content);

            if (truncated)
            {
                sb.AppendLine();
                sb.AppendLine("(Output truncated. Read more lines with offset parameter.)");
            }

            return sb.ToString();
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error reading file {FilePath}", filePath);
            return $"Tool call failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Creates a new file in the local file system.
    /// </summary>
    /// <param name="filePath">Path to the file to create.</param>
    /// <param name="content">Content to write to the file.</param>
    /// <returns>Success or error message.</returns>
    public async Task<string> CreateFileAsync(string filePath, string content)
    {
        try
        {
            var validPath = ValidatePath(filePath);

            if (File.Exists(validPath))
            {
                return "Tool call failed: File already exists. You must use an edit tool to modify it.";
            }

            // Auto-create parent directories
            var directory = Path.GetDirectoryName(validPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Strip leading filepath comment if present
            content = RemoveLeadingFilepathComment(content, filePath);

            // Normalize to \n line endings
            content = NormalizeLineEndings(content);

            await File.WriteAllTextAsync(validPath, content);

            return $"File created at {filePath}";
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error creating file {FilePath}", filePath);
            return $"Tool call failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Creates a directory in the local file system.
    /// </summary>
    /// <param name="dirPath">Path to the directory to create.</param>
    /// <returns>Success or error message.</returns>
    public string CreateDirectory(string dirPath)
    {
        try
        {
            var validPath = ValidatePath(dirPath);
            Directory.CreateDirectory(validPath);
            return $"Created directory at {dirPath}";
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error creating directory {DirPath}", dirPath);
            return $"Tool call failed: {ex.Message}";
        }
    }

    /// <inheritdoc />
    public Task<string> CreateDirectoryAsync(string dirPath) => Task.FromResult(CreateDirectory(dirPath));

    /// <summary>
    /// Lists the contents of a directory.
    /// </summary>
    /// <param name="path">Path to the directory.</param>
    /// <returns>Directory listing or error message.</returns>
    public string ListDirectory(string path)
    {
        try
        {
            var validPath = ValidatePath(path);

            if (!Directory.Exists(validPath))
            {
                return $"Tool call failed: Directory does not exist: {path}";
            }

            var entries = new List<string>();

            // Add files (no trailing slash)
            foreach (var file in Directory.GetFiles(validPath))
            {
                entries.Add(Path.GetFileName(file));
            }

            // Add directories (with trailing slash)
            foreach (var dir in Directory.GetDirectories(validPath))
            {
                entries.Add(Path.GetFileName(dir) + "/");
            }

            if (entries.Count == 0)
            {
                return "Folder is empty";
            }

            entries.Sort(StringComparer.Ordinal);
            return string.Join(Environment.NewLine, entries);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error listing directory {Path}", path);
            return $"Tool call failed: {ex.Message}";
        }
    }

    /// <inheritdoc />
    public Task<string> ListDirectoryAsync(string path) => Task.FromResult(ListDirectory(path));

    #endregion

    #region Edit Operations

    /// <summary>
    /// Replaces a string in a file.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <param name="oldString">The string to replace.</param>
    /// <param name="newString">The replacement string.</param>
    /// <returns>Success or error message.</returns>
    public async Task<string> ReplaceStringInFileAsync(string filePath, string oldString, string newString)
    {
        try
        {
            var validPath = ValidatePath(filePath);

            if (!File.Exists(validPath))
            {
                return $"Tool call failed: File does not exist: {filePath}. Use the create_file tool to create it.";
            }

            var content = await File.ReadAllTextAsync(validPath);

            // Normalize all line endings to \n for consistent processing
            var normalizedContent = NormalizeLineEndings(content);
            var normalizedOldString = NormalizeLineEndings(oldString);
            var normalizedNewString = NormalizeLineEndings(newString);

            // Count occurrences using ordinal comparison
            var count = CountOccurrences(normalizedContent, normalizedOldString);

            if (count == 0)
            {
                return "Tool call failed: String replacement failed: No match found for oldString";
            }

            if (count > 1)
            {
                return "Tool call failed: String replacement failed: Multiple matches found. Include more context to uniquely identify.";
            }

            // Perform the replacement
            var newContent = ReplaceFirst(normalizedContent, normalizedOldString, normalizedNewString);

            // Check if anything actually changed
            if (normalizedContent == newContent)
            {
                return "Tool call failed: No change was made. oldString and newString are identical.";
            }

            // Always write with \n line endings
            await File.WriteAllTextAsync(validPath, newContent);

            return $"Successfully replaced string in {filePath}";
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error replacing string in file {FilePath}", filePath);
            return $"Tool call failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Performs multiple replacement operations across files.
    /// </summary>
    /// <param name="explanation">Overall explanation for the multi-replace operation.</param>
    /// <param name="replacements">Array of replacement operations to perform.</param>
    /// <returns>Summary of successful and failed replacements.</returns>
    public async Task<string> MultiReplaceStringInFileAsync(string explanation, ReplaceOperation[] replacements)
    {
        if (replacements == null || replacements.Length == 0)
        {
            return "Tool call failed: No replacement operations provided";
        }

        var successes = new List<string>();
        var failures = new List<string>();

        foreach (var replacement in replacements)
        {
            var result = await ReplaceStringInFileAsync(
                replacement.FilePath,
                replacement.OldString,
                replacement.NewString);

            if (result.StartsWith("Tool call failed:"))
            {
                failures.Add($"- {replacement.FilePath}: {result.Replace("Tool call failed: ", "")}");
            }
            else
            {
                successes.Add($"- {replacement.FilePath}: {replacement.Explanation}");
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Multi-replace operation: {explanation}");
        sb.AppendLine();

        if (successes.Count > 0)
        {
            sb.AppendLine($"Successful ({successes.Count}):");
            foreach (var s in successes)
            {
                sb.AppendLine(s);
            }
        }

        if (failures.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Failed ({failures.Count}):");
            foreach (var f in failures)
            {
                sb.AppendLine(f);
            }
        }

        return sb.ToString().Trim();
    }

    #endregion

    #region Search Operations

    /// <summary>
    /// Searches for files matching a glob pattern.
    /// </summary>
    /// <param name="query">The glob pattern to search for.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <returns>Search results or error message.</returns>
    public string FileSearch(string query, int? maxResults = null)
    {
        try
        {
            var limit = maxResults ?? DefaultMaxSearchResults;

            // Normalize glob pattern
            var pattern = NormalizeGlobPattern(query);

            var matcher = new Matcher();
            matcher.AddInclude(pattern);

            var directoryInfo = new DirectoryInfoWrapper(new DirectoryInfo(_sandboxRoot));
            var result = matcher.Execute(directoryInfo);

            var files = result.Files
                .Take(limit)
                .Select(f => f.Path)
                .ToList();

            if (files.Count == 0)
            {
                return $"No files found matching pattern: {query}";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"{files.Count} result(s) found");
            foreach (var file in files)
            {
                sb.AppendLine(file);
            }

            return sb.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching files with pattern {Query}", query);
            return $"Tool call failed: {ex.Message}";
        }
    }

    /// <inheritdoc />
    public Task<string> FileSearchAsync(string query, int? maxResults = null) => Task.FromResult(FileSearch(query, maxResults));

    /// <summary>
    /// Performs a grep search in files.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="isRegexp">Whether the query is a regular expression.</param>
    /// <param name="includePattern">Optional glob pattern to filter files.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="includeIgnoredFiles">Whether to include normally ignored files.</param>
    /// <returns>JSON-serialized search results.</returns>
    public async Task<string> GrepSearchAsync(string query, bool isRegexp, string? includePattern = null,
                                               int? maxResults = null, bool includeIgnoredFiles = false)
    {
        try
        {
            var result = await GrepSearchStructuredAsync(query, isRegexp, includePattern, maxResults, includeIgnoredFiles);

            // Serialize to JSON for structured rendering
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            return JsonSerializer.Serialize(result, jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in grep search for {Query}", query);
            return $"Tool call failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Performs a structured grep search with match positions and context lines for rich UI rendering.
    /// </summary>
    public async Task<GrepSearchResult> GrepSearchStructuredAsync(
        string query,
        bool isRegexp,
        string? includePattern = null,
        int? maxResults = null,
        bool includeIgnoredFiles = false,
        int contextLines = 1)
    {
        var limit = maxResults ?? DefaultMaxSearchResults;
        var result = new GrepSearchResult
        {
            Query = query,
            IsRegex = isRegexp,
            MaxResults = limit,
            Files = new List<GrepFileResult>()
        };

        Regex? regex = null;
        if (isRegexp)
        {
            try
            {
                regex = new Regex(query, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            catch (ArgumentException)
            {
                return result;
            }
        }

        var filesToSearch = EnumerateSearchableFiles(includeIgnoredFiles, includePattern);
        var totalMatches = 0;

        foreach (var file in filesToSearch)
        {
            if (totalMatches >= limit)
            {
                result.IsTruncated = true;
                break;
            }

            // Skip binary files
            if (IsBinaryFile(file))
            {
                continue;
            }

            try
            {
                var lines = await File.ReadAllLinesAsync(file);
                var relativePath = Path.GetRelativePath(_sandboxRoot, file);
                var fileResult = new GrepFileResult
                {
                    FilePath = relativePath,
                    Matches = new List<GrepLineMatch>()
                };

                // Track which lines we've already added (to avoid duplicate context lines)
                var addedLineNumbers = new HashSet<int>();

                for (var i = 0; i < lines.Length && totalMatches < limit; i++)
                {
                    var line = lines[i];
                    var matchRanges = new List<MatchRange>();

                    if (isRegexp)
                    {
                        var matches = regex!.Matches(line);
                        foreach (Match m in matches)
                        {
                            matchRanges.Add(new MatchRange
                            {
                                Start = m.Index,
                                End = m.Index + m.Length
                            });
                        }
                    }
                    else
                    {
                        var index = 0;
                        while ((index = line.IndexOf(query, index, StringComparison.OrdinalIgnoreCase)) != -1)
                        {
                            matchRanges.Add(new MatchRange
                            {
                                Start = index,
                                End = index + query.Length
                            });
                            index += query.Length;
                        }
                    }

                    if (matchRanges.Count > 0)
                    {
                        // Add context lines before the match
                        for (var ctx = Math.Max(0, i - contextLines); ctx < i; ctx++)
                        {
                            if (!addedLineNumbers.Contains(ctx))
                            {
                                fileResult.Matches.Add(new GrepLineMatch
                                {
                                    LineNumber = ctx + 1,
                                    Content = TruncateLine(lines[ctx]),
                                    IsContext = true,
                                    MatchRanges = new List<MatchRange>()
                                });
                                addedLineNumbers.Add(ctx);
                            }
                        }

                        // Add the match line
                        if (!addedLineNumbers.Contains(i))
                        {
                            // Adjust match ranges if line was truncated
                            var truncatedLine = TruncateLine(line);
                            var adjustedRanges = matchRanges
                                .Where(r => r.Start < 200) // Only keep ranges that start within visible content
                                .Select(r => new MatchRange
                                {
                                    Start = r.Start,
                                    End = Math.Min(r.End, 200)
                                })
                                .ToList();

                            fileResult.Matches.Add(new GrepLineMatch
                            {
                                LineNumber = i + 1,
                                Content = truncatedLine,
                                IsContext = false,
                                MatchRanges = adjustedRanges
                            });
                            addedLineNumbers.Add(i);
                        }

                        // Add context lines after the match
                        for (var ctx = i + 1; ctx <= Math.Min(lines.Length - 1, i + contextLines); ctx++)
                        {
                            if (!addedLineNumbers.Contains(ctx))
                            {
                                fileResult.Matches.Add(new GrepLineMatch
                                {
                                    LineNumber = ctx + 1,
                                    Content = TruncateLine(lines[ctx]),
                                    IsContext = true,
                                    MatchRanges = new List<MatchRange>()
                                });
                                addedLineNumbers.Add(ctx);
                            }
                        }

                        totalMatches++;
                        fileResult.MatchCount++;
                    }
                }

                if (fileResult.Matches.Count > 0)
                {
                    // Sort matches by line number
                    fileResult.Matches = fileResult.Matches
                        .OrderBy(m => m.LineNumber)
                        .ToList();

                    result.Files.Add(fileResult);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Skipping file {File} due to error: {Error}", file, ex.Message);
            }
        }

        result.TotalMatches = totalMatches;

        // If no matches found with regex, try literal search
        if (result.TotalMatches == 0 && isRegexp)
        {
            _logger.LogDebug("No regex matches, trying literal search");
            return await GrepSearchStructuredAsync(query, false, includePattern, maxResults, includeIgnoredFiles, contextLines);
        }

        return result;
    }

    /// <summary>
    /// Enumerates files for search, applying exclusion filters.
    /// </summary>
    public IEnumerable<string> EnumerateSearchableFiles(bool includeIgnoredFiles, string? includePattern)
    {
        Matcher? includeMatcher = null;
        if (!string.IsNullOrEmpty(includePattern))
        {
            includeMatcher = new Matcher();
            includeMatcher.AddInclude(NormalizeGlobPattern(includePattern));
        }

        var codeRefsPath = Path.Combine(_sandboxRoot, "codeRefs");

        // Get files from git repos in codeRefs (respects .gitignore)
        foreach (var file in EnumerateCodeRefsFiles(codeRefsPath, includeIgnoredFiles))
        {
            if (ApplyFilters(file, includeMatcher))
            {
                yield return file;
            }
        }

        // Get files from non-codeRefs directories
        foreach (var file in EnumerateNonCodeRefsFiles(codeRefsPath, includeIgnoredFiles))
        {
            if (ApplyFilters(file, includeMatcher))
            {
                yield return file;
            }
        }
    }

    /// <summary>
    /// Enumerates files from git repositories in codeRefs/ using git ls-files.
    /// </summary>
    private IEnumerable<string> EnumerateCodeRefsFiles(string codeRefsPath, bool includeIgnoredFiles)
    {
        if (!Directory.Exists(codeRefsPath))
        {
            yield break;
        }

        // Find git repositories and non-git directories
        var gitRepos = new List<string>();
        var nonGitDirs = new List<string>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(codeRefsPath))
            {
                if (Directory.Exists(Path.Combine(dir, ".git")))
                {
                    gitRepos.Add(dir);
                }
                else
                {
                    nonGitDirs.Add(dir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Error scanning codeRefs: {Error}", ex.Message);
        }

        // Enumerate git repositories
        foreach (var repoPath in gitRepos)
        {
            IEnumerable<string>? gitFiles = null;

            if (!includeIgnoredFiles)
            {
                // Try to use git ls-files for accurate .gitignore handling
                gitFiles = GetGitTrackedFiles(repoPath);
            }

            if (gitFiles != null)
            {
                foreach (var file in gitFiles)
                {
                    yield return file;
                }
            }
            else
            {
                // Fallback: enumerate with default exclusions
                var enumOptions = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.System
                };

                foreach (var file in Directory.EnumerateFiles(repoPath, "*", enumOptions))
                {
                    var relativePath = Path.GetRelativePath(repoPath, file);
                    if (includeIgnoredFiles || !ShouldExcludePath(relativePath))
                    {
                        yield return file;
                    }
                }
            }
        }

        // Enumerate non-git directories
        foreach (var dirPath in nonGitDirs)
        {
            var enumOptions = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System
            };

            foreach (var file in Directory.EnumerateFiles(dirPath, "*", enumOptions))
            {
                var relativePath = Path.GetRelativePath(dirPath, file);
                if (includeIgnoredFiles || !ShouldExcludePath(relativePath))
                {
                    yield return file;
                }
            }
        }
    }

    /// <summary>
    /// Gets tracked files from a git repository using git ls-files.
    /// Returns null if git is not available or the command fails.
    /// </summary>
    private IEnumerable<string>? GetGitTrackedFiles(string repoPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "ls-files --cached --others --exclude-standard",
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                return null;
            }

            return output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => Path.Combine(repoPath, f.Trim().Replace('/', Path.DirectorySeparatorChar)))
                .Where(File.Exists); // Filter out deleted files
        }
        catch (Exception ex)
        {
            _logger.LogDebug("git ls-files failed for {Repo}: {Error}", repoPath, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Enumerates files from directories outside codeRefs/.
    /// </summary>
    private IEnumerable<string> EnumerateNonCodeRefsFiles(string codeRefsPath, bool includeIgnoredFiles)
    {
        var enumOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System
        };

        // Enumerate top-level directories except codeRefs
        foreach (var dir in Directory.EnumerateDirectories(_sandboxRoot))
        {
            var dirName = Path.GetFileName(dir);

            // Skip codeRefs (handled separately) and hidden directories
            if (dirName.Equals("codeRefs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!includeIgnoredFiles && (dirName.StartsWith('.') || DefaultExcludedFolders.Contains(dirName)))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(dir, "*", enumOptions))
            {
                var relativePath = Path.GetRelativePath(dir, file);
                if (includeIgnoredFiles || !ShouldExcludePath(relativePath))
                {
                    yield return file;
                }
            }
        }

        // Also include top-level files in SandboxRoot
        foreach (var file in Directory.EnumerateFiles(_sandboxRoot))
        {
            yield return file;
        }
    }

    /// <summary>
    /// Applies include pattern and binary file filters.
    /// </summary>
    private bool ApplyFilters(string file, Matcher? includeMatcher)
    {
        // Skip binary files
        if (IsBinaryFile(file))
        {
            return false;
        }

        // Apply include pattern filter if specified
        if (includeMatcher != null)
        {
            var relativePath = Path.GetRelativePath(_sandboxRoot, file);
            var match = includeMatcher.Match(relativePath);
            if (!match.HasMatches)
            {
                return false;
            }
        }

        return true;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Normalizes line endings to \n (LF) for consistent cross-platform behavior.
    /// </summary>
    public static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    /// <summary>
    /// Removes leading filepath comments that LLMs sometimes add to generated code.
    /// </summary>
    public static string RemoveLeadingFilepathComment(string content, string filePath)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        var lines = content.Split('\n');
        if (lines.Length == 0)
        {
            return content;
        }

        var firstLine = lines[0].Trim();
        var fileName = Path.GetFileName(filePath);

        // Check for common patterns like "// src/foo.ts" or "# path/to/file.py"
        if (firstLine.StartsWith("//") || firstLine.StartsWith("#"))
        {
            var commentContent = firstLine.TrimStart('/', '#', ' ');
            if (commentContent.EndsWith(fileName, StringComparison.OrdinalIgnoreCase) ||
                commentContent.Contains(filePath, StringComparison.OrdinalIgnoreCase))
            {
                return string.Join('\n', lines.Skip(1));
            }
        }

        return content;
    }

    /// <summary>
    /// Counts occurrences of a pattern in text.
    /// </summary>
    public static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    /// <summary>
    /// Replaces the first occurrence of a string.
    /// </summary>
    public static string ReplaceFirst(string text, string oldValue, string newValue)
    {
        var index = text.IndexOf(oldValue, StringComparison.Ordinal);
        if (index < 0)
        {
            return text;
        }
        return text.Substring(0, index) + newValue + text.Substring(index + oldValue.Length);
    }

    /// <summary>
    /// Normalizes a glob pattern for searching.
    /// </summary>
    public static string NormalizeGlobPattern(string pattern)
    {
        // Prepend **/ if pattern doesn't start with ** or /
        if (!pattern.StartsWith("**/") && !pattern.StartsWith("/"))
        {
            pattern = "**/" + pattern;
        }

        // Append ** if pattern ends with /
        if (pattern.EndsWith("/"))
        {
            pattern += "**";
        }

        return pattern;
    }

    /// <summary>
    /// Checks if a file is binary based on extension.
    /// </summary>
    public static bool IsBinaryFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(ext) && BinaryExtensions.Contains(ext);
    }

    /// <summary>
    /// Checks if a path should be excluded based on folder patterns.
    /// </summary>
    public static bool ShouldExcludePath(string relativePath)
    {
        // Split path into segments and check each folder
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var segment in segments)
        {
            if (DefaultExcludedFolders.Contains(segment))
            {
                return true;
            }

            // Exclude hidden folders (starting with .)
            if (segment.StartsWith('.') && segment.Length > 1)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Truncates a line to a maximum length for display.
    /// </summary>
    public static string TruncateLine(string line, int maxLength = 200)
    {
        return line.Length > maxLength ? line[..maxLength] + "..." : line;
    }

    #endregion
}

#region Search Result Models

/// <summary>
/// Structured result from a grep search operation for rich UI rendering.
/// </summary>
public class GrepSearchResult
{
    /// <summary>
    /// Total number of matches found across all files.
    /// </summary>
    public int TotalMatches { get; set; }

    /// <summary>
    /// The search query used.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Whether the query was a regex pattern.
    /// </summary>
    public bool IsRegex { get; set; }

    /// <summary>
    /// Grouped results by file.
    /// </summary>
    public List<GrepFileResult> Files { get; set; } = new();

    /// <summary>
    /// Whether results were truncated due to limit.
    /// </summary>
    public bool IsTruncated { get; set; }

    /// <summary>
    /// The maximum results limit that was applied.
    /// </summary>
    public int MaxResults { get; set; }
}

/// <summary>
/// Results for a single file in a grep search.
/// </summary>
public class GrepFileResult
{
    /// <summary>
    /// Relative path to the file from sandbox root.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Number of matches in this file.
    /// </summary>
    public int MatchCount { get; set; }

    /// <summary>
    /// Individual line matches with context.
    /// </summary>
    public List<GrepLineMatch> Matches { get; set; } = new();
}

/// <summary>
/// A single line match or context line in grep results.
/// </summary>
public class GrepLineMatch
{
    /// <summary>
    /// 1-based line number of the match.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// The full line content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a context line (not a match line).
    /// </summary>
    public bool IsContext { get; set; }

    /// <summary>
    /// Start and end positions of matches within the line (for highlighting).
    /// </summary>
    public List<MatchRange> MatchRanges { get; set; } = new();
}

/// <summary>
/// Character range for highlighting a match within a line.
/// </summary>
public class MatchRange
{
    /// <summary>
    /// 0-based start character index.
    /// </summary>
    public int Start { get; set; }

    /// <summary>
    /// 0-based end character index (exclusive).
    /// </summary>
    public int End { get; set; }
}


#endregion
