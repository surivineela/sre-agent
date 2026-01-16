// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.Models.WorkspaceTools;
using Agent.Plugins.Services;
using HtmlAgilityPack;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

/// <summary>
/// Implementation of VS Code-like agent tools.
/// Clones behavior from CopilotChat Node.js implementation.
/// </summary>
public class WorkspaceToolsPlugin : IWorkspaceToolsPlugin, IDisposable
{
    private readonly ILogger<WorkspaceToolsPlugin> _logger;
    private readonly TerminalSessionManager _terminalManager;
    private readonly IHttpClientFactory _httpClientFactory;

    // Todo list storage keyed by ThreadId (persisted across requests for the same thread)
    private static readonly ConcurrentDictionary<Guid, List<WorkspaceTodoItem>> _todoLists = new();

    /// <summary>
    /// Gets the current thread ID from ThreadContextAccessor, or falls back to Guid.Empty if not set.
    /// </summary>
    private static Guid CurrentThreadKey => ThreadContextAccessor.CurrentThreadId ?? Guid.Empty;

    // Configuration
    private const int MaxLinesPerRead = 2000;
    private const int DefaultMaxSearchResults = 20;

    private static string InitializeSandboxRoot()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SreAgent", "TerminalRoot");
        }
        else
        {
            // Linux/macOS
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "sreagent", "terminalRoot");
        }
    }

    /// <summary>
    /// Sandbox root path - all file operations must be within this path.
    /// <summary>
    /// Gets the root directory for sandbox operations.
    /// </summary>
    public static readonly string SandboxRoot = InitializeSandboxRoot();
    public static readonly string CodeRefsPath = Path.Combine(SandboxRoot, "codeRefs");
    public static readonly string TmpPath = Path.Combine(SandboxRoot, "tmp");

    /// <inheritdoc />
    public bool Enabled { get; }

    public WorkspaceToolsPlugin(
        ILogger<WorkspaceToolsPlugin> logger,
        TerminalSessionManager terminalManager,
        IHttpClientFactory httpClientFactory,
        ExperimentalSettings experimentalSettings,
        IExperimentLoader experimentLoader)
    {
        _logger = logger;
        _terminalManager = terminalManager;
        _httpClientFactory = httpClientFactory;
        // Enable if either the setting is true OR the experiment feature flag is enabled
        Enabled = experimentalSettings.EnableWorkspaceTools
            || experimentLoader.IsFeatureFlagEnabled(Constants.FeatureFlags.EnableWorkspaceTools);

        if (Enabled)
        {
            EnsureWorkspaceSetup();
        }
    }

    #region State Accessors (for IAmbientContextProvider)

    /// <inheritdoc />
    public IReadOnlyList<WorkspaceTodoItem>? GetTodoList()
    {
        var threadKey = ThreadContextAccessor.CurrentThreadId ?? Guid.Empty;
        return _todoLists.TryGetValue(threadKey, out var todos) ? todos : null;
    }

    /// <inheritdoc />
    public string GetTerminalStateForContext() => _terminalManager.GetTerminalStateForContext();

    private void EnsureWorkspaceSetup()
    {
        if (!Directory.Exists(CodeRefsPath))
        {
            Directory.CreateDirectory(CodeRefsPath);
            _logger.LogInternalInformation($"Created codeRefs directory: {CodeRefsPath}");
        }

        if (!Directory.Exists(TmpPath))
        {
            Directory.CreateDirectory(TmpPath);
            _logger.LogInternalInformation($"Created tmp directory: {TmpPath}");
        }
    }

    #endregion

    #region Path Validation

    private static string ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty");
        }

        // Convert relative paths to absolute paths relative to SandboxRoot
        string fullPath;
        if (!Path.IsPathRooted(path))
        {
            fullPath = Path.GetFullPath(Path.Combine(SandboxRoot, path));
        }
        else
        {
            fullPath = Path.GetFullPath(path);
        }

        if (!fullPath.StartsWith(SandboxRoot, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"Path must be within sandbox root: {SandboxRoot}");
        }

        return fullPath;
    }

    #endregion

    #region File Operations

    public async Task<string> ReadFileAsync(string filePath, int startLine, int endLine)
    {
        try
        {
            var validPath = ValidatePath(filePath);

            if (!File.Exists(validPath))
            {
                return $"Tool call failed: File does not exist: {filePath}";
            }

            // Swap if start > end (forgiving behavior from Node impl)
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
            _logger.LogInternalError(ex, "Error reading file {FilePath}", filePath);
            return $"Tool call failed: {ex.Message}";
        }
    }

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

            // Strip leading filepath comment if present (matches Node impl)
            content = RemoveLeadingFilepathComment(content, filePath);

            // Normalize to \n line endings
            content = NormalizeLineEndings(content);

            await File.WriteAllTextAsync(validPath, content);

            return $"File created at {filePath}";
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogInternalError(ex, "Error creating file {FilePath}", filePath);
            return $"Tool call failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Removes leading filepath comments that LLMs sometimes add to generated code.
    /// </summary>
    private static string RemoveLeadingFilepathComment(string content, string filePath)
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

    public Task<string> CreateDirectoryAsync(string dirPath)
    {
        try
        {
            var validPath = ValidatePath(dirPath);
            Directory.CreateDirectory(validPath);
            return Task.FromResult($"Created directory at {dirPath}");
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogInternalError(ex, "Error creating directory {DirPath}", dirPath);
            return Task.FromResult($"Tool call failed: {ex.Message}");
        }
    }

    public Task<string> ListDirectoryAsync(string path)
    {
        try
        {
            var validPath = ValidatePath(path);

            if (!Directory.Exists(validPath))
            {
                return Task.FromResult($"Tool call failed: Directory does not exist: {path}");
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
                return Task.FromResult("Folder is empty");
            }

            entries.Sort(StringComparer.Ordinal);
            return Task.FromResult(string.Join(Environment.NewLine, entries));
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogInternalError(ex, "Error listing directory {Path}", path);
            return Task.FromResult($"Tool call failed: {ex.Message}");
        }
    }

    #endregion

    #region Edit Operations

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
            _logger.LogInternalError(ex, "Error replacing string in file {FilePath}", filePath);
            return $"Tool call failed: {ex.Message}";
        }
    }

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

    /// <summary>
    /// Normalizes line endings to \n (LF) for consistent cross-platform behavior.
    /// </summary>
    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    private static int CountOccurrences(string text, string pattern)
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

    private static string ReplaceFirst(string text, string oldValue, string newValue)
    {
        var index = text.IndexOf(oldValue, StringComparison.Ordinal);
        if (index < 0)
        {
            return text;
        }
        return text.Substring(0, index) + newValue + text.Substring(index + oldValue.Length);
    }

    #endregion

    #region Search Operations

    public Task<string> FileSearchAsync(string query, int? maxResults = null)
    {
        try
        {
            var limit = maxResults ?? DefaultMaxSearchResults;

            // Normalize glob pattern as per Node implementation
            var pattern = NormalizeGlobPattern(query);

            var matcher = new Matcher();
            matcher.AddInclude(pattern);

            var directoryInfo = new DirectoryInfoWrapper(new DirectoryInfo(SandboxRoot));
            var result = matcher.Execute(directoryInfo);

            var files = result.Files
                .Take(limit)
                .Select(f => f.Path)
                .ToList();

            if (files.Count == 0)
            {
                return Task.FromResult($"No files found matching pattern: {query}");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"{files.Count} result(s) found");
            foreach (var file in files)
            {
                sb.AppendLine(file);
            }

            return Task.FromResult(sb.ToString().Trim());
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error searching files with pattern {Query}", query);
            return Task.FromResult($"Tool call failed: {ex.Message}");
        }
    }

    public async Task<string> GrepSearchAsync(string query, bool isRegexp, string? includePattern = null,
                                               int? maxResults = null, bool includeIgnoredFiles = false)
    {
        try
        {
            var result = await GrepSearchStructuredAsync(query, isRegexp, includePattern, maxResults, includeIgnoredFiles);

            // Serialize to JSON for structured rendering in frontend
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            return JsonSerializer.Serialize(result, jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in grep search for {Query}", query);
            return $"Tool call failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Performs a structured grep search with match positions and context lines for rich UI rendering.
    /// </summary>
    private async Task<GrepSearchResult> GrepSearchStructuredAsync(
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
                var relativePath = Path.GetRelativePath(SandboxRoot, file);
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
                _logger.LogInternalDebug("Skipping file {File} due to error: {Error}", file, ex.Message);
            }
        }

        result.TotalMatches = totalMatches;

        // If no matches found with regex, try literal search
        if (result.TotalMatches == 0 && isRegexp)
        {
            _logger.LogInternalDebug("No regex matches, trying literal search");
            return await GrepSearchStructuredAsync(query, false, includePattern, maxResults, includeIgnoredFiles, contextLines);
        }

        return result;
    }

    /// <summary>
    /// Truncates a line to a maximum length for display.
    /// </summary>
    private static string TruncateLine(string line, int maxLength = 200)
    {
        return line.Length > maxLength ? line[..maxLength] + "..." : line;
    }

    /// <summary>
    /// Default folders to exclude from search (matches VS Code search.exclude defaults).
    /// </summary>
    private static readonly HashSet<string> DefaultExcludedFolders = new(StringComparer.OrdinalIgnoreCase)
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
    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
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
    /// Enumerates files for search, applying exclusion filters.
    /// For git repos in codeRefs/, uses git ls-files to respect .gitignore.
    /// </summary>
    private IEnumerable<string> EnumerateSearchableFiles(bool includeIgnoredFiles, string? includePattern)
    {
        Matcher? includeMatcher = null;
        if (!string.IsNullOrEmpty(includePattern))
        {
            includeMatcher = new Matcher();
            includeMatcher.AddInclude(NormalizeGlobPattern(includePattern));
        }

        // Get files from git repos in codeRefs (respects .gitignore)
        foreach (var file in EnumerateCodeRefsFiles(includeIgnoredFiles))
        {
            if (ApplyFilters(file, includeMatcher))
            {
                yield return file;
            }
        }

        // Get files from tmp/ and other non-codeRefs directories
        foreach (var file in EnumerateNonCodeRefsFiles(includeIgnoredFiles))
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
    private IEnumerable<string> EnumerateCodeRefsFiles(bool includeIgnoredFiles)
    {
        if (!Directory.Exists(CodeRefsPath))
        {
            yield break;
        }

        // Find git repositories and non-git directories
        var gitRepos = new List<string>();
        var nonGitDirs = new List<string>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(CodeRefsPath))
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
            _logger.LogInternalDebug("Error scanning codeRefs: {Error}", ex.Message);
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
            _logger.LogInternalDebug("git ls-files failed for {Repo}: {Error}", repoPath, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Enumerates files from directories outside codeRefs/ (like tmp/).
    /// </summary>
    private IEnumerable<string> EnumerateNonCodeRefsFiles(bool includeIgnoredFiles)
    {
        var enumOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System
        };

        // Enumerate top-level directories except codeRefs
        foreach (var dir in Directory.EnumerateDirectories(SandboxRoot))
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
        foreach (var file in Directory.EnumerateFiles(SandboxRoot))
        {
            yield return file;
        }
    }

    /// <summary>
    /// Applies include pattern and binary file filters.
    /// </summary>
    private static bool ApplyFilters(string file, Matcher? includeMatcher)
    {
        // Skip binary files
        if (IsBinaryFile(file))
        {
            return false;
        }

        // Apply include pattern filter if specified
        if (includeMatcher != null)
        {
            var relativePath = Path.GetRelativePath(SandboxRoot, file);
            var match = includeMatcher.Match(relativePath);
            if (!match.HasMatches)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if a path should be excluded based on folder patterns.
    /// </summary>
    private static bool ShouldExcludePath(string relativePath)
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
    /// Checks if a file is binary based on extension.
    /// </summary>
    private static bool IsBinaryFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(ext) && BinaryExtensions.Contains(ext);
    }

    private static string NormalizeGlobPattern(string pattern)
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

    #endregion

    #region Terminal Operations

    public async Task<string> RunInTerminalAsync(string command, string explanation, bool isBackground)
    {
        try
        {
            if (isBackground)
            {
                var result = await _terminalManager.ExecuteBackgroundCommandAsync(command);
                return result;
            }
            else
            {
                var (output, exitCode) = await _terminalManager.ExecuteCommandAsync(command);
                return FormatForegroundOutput(output, exitCode);
            }
        }
        catch (TimeoutException)
        {
            return "Tool call failed: Command timed out after 5 minutes";
        }
        catch (TerminalInitializationException ex)
        {
            _logger.LogInternalError(ex, "Failed to initialize terminal session");
            return $"Tool call failed: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error running command in terminal: {Command}", command);
            return $"Tool call failed: {ex.Message}";
        }
    }

    private static string FormatForegroundOutput(string output, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return $"Command completed with exit code {exitCode}";
        }

        return $"{output}\n\nExit code: {exitCode}";
    }

    public Task<string> GetTerminalLastCommandAsync()
    {
        var lastCommand = _terminalManager.GetLastCommand();
        if (string.IsNullOrEmpty(lastCommand))
        {
            return Task.FromResult("Tool call failed: No active terminal session or no command has been run");
        }

        return Task.FromResult(lastCommand);
    }

    #endregion

    #region Task Management

    public Task<string> ManageTodoListAsync(string operation, WorkspaceTodoItem[]? todoList = null)
    {
        try
        {
            if (string.Equals(operation, "read", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(HandleTodoRead());
            }

            if (string.Equals(operation, "write", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(HandleTodoWrite(todoList));
            }

            return Task.FromResult($"Tool call failed: Invalid operation: {operation}. Must be 'read' or 'write'.");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error managing todo list");
            return Task.FromResult($"Tool call failed: {ex.Message}");
        }
    }

    private string HandleTodoRead()
    {
        var threadKey = CurrentThreadKey;
        if (!_todoLists.TryGetValue(threadKey, out var todos) || todos.Count == 0)
        {
            return "No todo list found.";
        }

        return FormatTodoListAsMarkdown(todos);
    }

    private string HandleTodoWrite(WorkspaceTodoItem[]? todoList)
    {
        // Match VS Code: todoList is required for write operation
        if (todoList == null)
        {
            return "Tool call failed: todoList is required for write operation";
        }

        var warnings = new List<string>();

        // Validate and warn as per VS Code implementation
        if (todoList.Length < 3)
        {
            warnings.Add("Warning: Small todo list (<3 items). This task might not need a todo list.");
        }
        else if (todoList.Length > 10)
        {
            warnings.Add("Warning: Large todo list (>10 items). Consider keeping the list focused and actionable.");
        }

        var inProgressCount = todoList.Count(t => t.Status == "in-progress");
        if (inProgressCount > 1)
        {
            warnings.Add("Warning: Multiple todos marked as in-progress. Consider focusing on one at a time.");
        }

        // Validate sequential completion order
        for (int i = 0; i < todoList.Length; i++)
        {
            var current = todoList[i];

            // Warn if not-started task has any subsequent task that's started
            if (current.Status == "not-started")
            {
                var nextStartedTask = todoList.Skip(i + 1).FirstOrDefault(t => t.Status != "not-started");
                if (nextStartedTask != null)
                {
                    warnings.Add($"Warning: Todo #{current.Id} is 'not-started' but todo #{nextStartedTask.Id} is '{nextStartedTask.Status}'. Complete previous steps first, or update todos if step #{current.Id} is no longer needed.");
                }
            }

            // Warn if in-progress task has any subsequent task that's not not-started
            if (current.Status == "in-progress")
            {
                var nextStartedTask = todoList.Skip(i + 1).FirstOrDefault(t => t.Status != "not-started");
                if (nextStartedTask != null)
                {
                    warnings.Add($"Warning: Todo #{current.Id} is 'in-progress' but todo #{nextStartedTask.Id} is '{nextStartedTask.Status}'. Complete all tasks sequentially.");
                }
            }
        }

        var threadKey = CurrentThreadKey;

        // Check for many simultaneous updates
        if (_todoLists.TryGetValue(threadKey, out var existing) && existing.Count > 0)
        {
            var changedCount = CalculateTodoChanges(existing, todoList.ToList());
            if (changedCount > 3)
            {
                warnings.Add("Warning: Did you mean to update so many todos at the same time? Consider working on them one by one.");
            }
        }

        _todoLists[threadKey] = todoList.ToList();

        var result = "Successfully wrote todo list";
        if (warnings.Count > 0)
        {
            result += "\n\n" + string.Join("\n", warnings);
        }

        return result;
    }

    /// <summary>
    /// Calculate number of changes between old and new todo lists.
    /// Matches VS Code implementation: compares index-by-index.
    /// </summary>
    private static int CalculateTodoChanges(List<WorkspaceTodoItem> oldList, List<WorkspaceTodoItem> newList)
    {
        // Assume arrays are equivalent in order; compare index-by-index
        var modified = 0;
        var minLen = Math.Min(oldList.Count, newList.Count);

        for (var i = 0; i < minLen; i++)
        {
            var o = oldList[i];
            var n = newList[i];
            if (o.Title != n.Title ||
                (o.Description ?? "") != (n.Description ?? "") ||
                o.Status != n.Status)
            {
                modified++;
            }
        }

        var added = Math.Max(0, newList.Count - oldList.Count);
        var removed = Math.Max(0, oldList.Count - newList.Count);
        var totalChanges = added + removed + modified;
        return totalChanges;
    }

    private static string FormatTodoListAsMarkdown(List<WorkspaceTodoItem> todos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Todo List");
        sb.AppendLine();

        foreach (var todo in todos.OrderBy(t => t.Id))
        {
            var checkbox = todo.Status switch
            {
                "completed" => "[x]",
                "in-progress" => "[-]",
                _ => "[ ]"
            };

            sb.AppendLine($"- {checkbox} {todo.Title}");
            if (!string.IsNullOrWhiteSpace(todo.Description))
            {
                sb.AppendLine($"  - {todo.Description}");
            }
        }

        return sb.ToString().Trim();
    }

    #endregion

    #region Web Operations

    public async Task<string> FetchWebpageAsync(string[] urls, string query)
    {
        if (urls == null || urls.Length == 0)
        {
            return "Tool call failed: No URLs provided";
        }

        var results = new List<string>();

        foreach (var url in urls)
        {
            try
            {
                var content = await FetchSinglePageAsync(url);
                results.Add($"Content from {url}:\n{content}");
            }
            catch (Exception ex)
            {
                results.Add($"Content from {url}:\nError fetching page: {ex.Message}");
            }
        }

        return string.Join("\n\n---\n\n", results);
    }

    private async Task<string> FetchSinglePageAsync(string url)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();

        // Parse HTML and extract main content
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Remove script and style elements
        var nodesToRemove = doc.DocumentNode.SelectNodes("//script|//style|//nav|//header|//footer|//aside");
        if (nodesToRemove != null)
        {
            foreach (var node in nodesToRemove.ToList())
            {
                node.Remove();
            }
        }

        // Try to find main content area
        var mainContent = doc.DocumentNode.SelectSingleNode("//main") ??
                          doc.DocumentNode.SelectSingleNode("//article") ??
                          doc.DocumentNode.SelectSingleNode("//div[@id='content']") ??
                          doc.DocumentNode.SelectSingleNode("//div[@class='content']") ??
                          doc.DocumentNode.SelectSingleNode("//body");

        if (mainContent == null)
        {
            return "Could not extract content from page";
        }

        // Get text content and clean it up
        var text = mainContent.InnerText;

        // Clean up whitespace
        text = Regex.Replace(text, @"\s+", " ");
        text = text.Trim();

        // Truncate if too long (10KB limit for web content)
        const int maxLength = 10 * 1024;
        if (text.Length > maxLength)
        {
            text = text.Substring(0, maxLength) + "\n\n[Content truncated...]";
        }

        return text;
    }

    #endregion

    public void Dispose()
    {
        _terminalManager.Dispose();
        GC.SuppressFinalize(this);
    }
}
