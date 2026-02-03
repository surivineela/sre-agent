// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Agent.Common.Services;
using Agent.Core.Services;
using Agent.Framework;
using Agent.Plugins.Models.WorkspaceTools;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Services;

#region Workspace Context Records

/// <summary>
/// Result of loading instruction files - contains both full-content files
/// (copilot-instructions.md, AGENTS.md) and metadata-only files (*.instructions.md).
/// </summary>
public record InstructionFilesResult(
    IReadOnlyList<InstructionFileContent> FullContentFiles,
    IReadOnlyList<InstructionFileMetadata> MetadataOnlyFiles);

public record InstructionFileContent(string Path, string Content);

public record InstructionFileMetadata(string Path, string? Description, string? ApplyTo);

public record GitRepositoryInfo(string Name, string Owner, string CurrentBranch, string DefaultBranch);

#endregion

/// <summary>
/// Local implementation of IWorkspaceContext.
/// Reads files and builds context from the local file system.
/// Used by both WorkspaceToolsPlugin (local mode) and gRPC server.
/// </summary>
public class LocalWorkspaceContext : IWorkspaceContext
{
    private readonly ILogger _logger;
    private readonly ISandboxPaths _sandboxPaths;

    // Ambient context configuration
    private const int MaxCharsPerInstructionFile = 5000;
    private const int MaxTotalInstructionChars = 25000;
    private const int MaxDirectoryTreeLength = 2000;

    public LocalWorkspaceContext(
        ILogger logger,
        ISandboxPaths sandboxPaths)
    {
        _logger = logger;
        _sandboxPaths = sandboxPaths;
    }

    #region IWorkspaceContext Implementation

    /// <inheritdoc />
    public async Task<string> GetInstructionsContextAsync(CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        var result = await LoadInstructionFilesAsync(ct);

        if (result.FullContentFiles.Count == 0 && result.MetadataOnlyFiles.Count == 0)
        {
            return string.Empty;
        }

        // 1. Add full-content instruction files with truncation
        var totalCharsUsed = 0;
        foreach (var file in result.FullContentFiles)
        {
            var truncatedContent = file.Content;
            var isTruncated = false;

            if (file.Content.Length > MaxCharsPerInstructionFile)
            {
                truncatedContent = file.Content[..MaxCharsPerInstructionFile];
                truncatedContent += "\n\n[Content truncated. Use ReadFile tool to see full content.]";
                isTruncated = true;
            }

            if (totalCharsUsed + truncatedContent.Length > MaxTotalInstructionChars)
            {
                // Would exceed total budget
                truncatedContent = file.Content[..(MaxTotalInstructionChars - totalCharsUsed)];
                truncatedContent += "\n\n[Content truncated. Use ReadFile tool to see full content.]";
                isTruncated = true;
            }

            if (isTruncated)
            {
                sb.AppendLine($"<attachment filePath=\"{file.Path}\" isTruncated=\"true\">");
            }
            else
            {
                sb.AppendLine($"<attachment filePath=\"{file.Path}\">");
            }

            sb.AppendLine(truncatedContent);
            sb.AppendLine("</attachment>");

            totalCharsUsed += truncatedContent.Length;
        }

        // 2. Add instruction file list with metadata only (for *.instructions.md files)
        if (result.MetadataOnlyFiles.Count > 0)
        {
            sb.AppendLine("<instructions>");
            sb.AppendLine("Here is a list of instruction files that contain rules for modifying or creating new code.");
            sb.AppendLine("These files are important for ensuring that the code is modified or created correctly.");
            sb.AppendLine("Please make sure to follow the rules specified in these files when working with the codebase.");
            sb.AppendLine("If the file is not already available as attachment, use the 'ReadFile' tool to acquire it.");
            sb.AppendLine("Make sure to acquire the instructions before making any changes to the code.");

            foreach (var file in result.MetadataOnlyFiles)
            {
                sb.AppendLine("<instruction>");
                sb.AppendLine($"<file>{file.Path}</file>");
                if (!string.IsNullOrEmpty(file.ApplyTo))
                {
                    sb.AppendLine($"<applyTo>{file.ApplyTo}</applyTo>");
                }

                if (!string.IsNullOrEmpty(file.Description))
                {
                    sb.AppendLine($"<description>{file.Description}</description>");
                }

                sb.AppendLine("</instruction>");
            }

            sb.AppendLine("</instructions>");
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public async Task<string> GetEnvironmentContextAsync(CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        var sandboxPaths = await _sandboxPaths.GetSandboxPathsAsync();

        // 1. Environment info
        sb.AppendLine("<environment_info>");
        sb.AppendLine($"The user's current OS is: {GetOperatingSystemName()}");
        sb.AppendLine("</environment_info>");

        // 2. Workspace info (SandboxRoot structure)
        sb.AppendLine("<workspace_info>");
        sb.AppendLine("I am working in a workspace with the following folders:");
        sb.AppendLine($"- {sandboxPaths.SandboxRoot}");
        sb.AppendLine();
        sb.AppendLine("The workspace has these special directories:");
        sb.AppendLine("- codeRefs/: Contains code repositories the user has attached for context.");
        sb.AppendLine("- tmp/: For throwaway work, scratch files, and temporary outputs.");
        sb.AppendLine($"- memories/sessionInsights/{ThreadContextAccessor.CurrentThreadId}: For any findings, learnings, or insights you want to persist.");
        sb.AppendLine($"- memories/synthesizedKnowledge: Contains knowledge from previous sessions ");
        sb.AppendLine();
        sb.AppendLine("I am working in a workspace that has the following structure:");
        sb.AppendLine("```");
        sb.AppendLine(await BuildDirectoryTreeAsync(sandboxPaths.SandboxRoot, MaxDirectoryTreeLength, ct));
        sb.AppendLine("```");
        sb.AppendLine("This is the state of the context at this point. The view may be truncated.");
        sb.AppendLine("</workspace_info>");

        return sb.ToString();
    }

    /// <inheritdoc />
    public async Task<string> GetPreUserQueryContextAsync(
        string? terminalState,
        IReadOnlyList<WorkspaceTodoItemDto>? todoList,
        CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        var sandboxPaths = await _sandboxPaths.GetSandboxPathsAsync();

        // 1. Git repository attachments
        var gitRepos = await GetGitRepositoriesAsync(sandboxPaths.SandboxRoot, ct);
        if (gitRepos.Count > 0)
        {
            sb.AppendLine("<attachments>");
            foreach (var repo in gitRepos)
            {
                sb.AppendLine($"<attachment id=\"{repo.Owner}/{repo.Name}\">");
                sb.AppendLine("Information about one of the current repositories. You can use this information when you need to calculate diffs or compare changes with the default branch:");
                sb.AppendLine($"Repository name: {repo.Name}");
                sb.AppendLine($"Owner: {repo.Owner}");
                sb.AppendLine($"Current branch: {repo.CurrentBranch}");
                sb.AppendLine($"Default branch: {repo.DefaultBranch}");
                sb.AppendLine("</attachment>");
            }

            sb.AppendLine("</attachments>");
        }

        // 2. Context: date + terminal
        sb.AppendLine("<context>");
        sb.AppendLine($"The current date and time is {DateTimeOffset.UtcNow:MMMM d, yyyy h:mm tt} UTC.");

        if (!string.IsNullOrEmpty(terminalState))
        {
            sb.AppendLine();
            sb.AppendLine(terminalState);
        }

        sb.AppendLine("</context>");

        // 3. Tool usage reminders (always included)
        sb.AppendLine("<reminder_tool>");
        sb.AppendLine("When using the ReplaceStringInFile tool, include 3-5 lines of unchanged code before and after the string you want to replace, to make it unambiguous which part of the file should be edited.");
        sb.AppendLine("For maximum efficiency, whenever you plan to perform multiple independent edit operations, invoke them simultaneously using MultiReplaceStringInFile tool rather than sequentially.");
        sb.AppendLine("Do NOT create a new markdown file to document each change or summarize your work unless specifically requested by the user.");
        sb.AppendLine("</reminder_tool>");

        // 4. Plan reminder (format todo list internally)
        var todoReminder = BuildPlanReminder(todoList);
        if (!string.IsNullOrEmpty(todoReminder))
        {
            sb.AppendLine("<reminder_todo>");
            sb.AppendLine(todoReminder);
            sb.AppendLine("</reminder_todo>");
        }

        return sb.ToString();
    }

    #endregion

    #region Public Static Helpers

    /// <summary>
    /// Builds plan reminder from a todo list.
    /// Formats todos in a structured way matching Copilot Chat's todoList context.
    /// This is public static so it can be called by both client and gRPC server.
    /// </summary>
    public static string BuildPlanReminder(IReadOnlyList<WorkspaceTodoItemDto>? todos)
    {
        if (todos == null || todos.Count == 0)
        {
            return "This is a reminder that your todo list is currently empty. " +
                "DO NOT mention this to the user explicitly because they are already aware. " +
                "If you are working on tasks that would benefit from a todo list please use the ManageTodoList tool to create one. " +
                "If not, please feel free to ignore. Again do not mention this message to the user.";
        }

        var completed = todos.Count(t => string.Equals(t.Status, "completed", StringComparison.OrdinalIgnoreCase));
        var inProgress = todos.Count(t => string.Equals(t.Status, "in-progress", StringComparison.OrdinalIgnoreCase));
        var notStarted = todos.Count(t => string.Equals(t.Status, "not-started", StringComparison.OrdinalIgnoreCase));
        var total = todos.Count;

        var sb = new StringBuilder();
        sb.AppendLine("<todoList>");
        sb.AppendLine($"Progress: {completed}/{total} completed, {inProgress} in-progress, {notStarted} not-started");
        sb.AppendLine();

        foreach (var todo in todos.OrderBy(t => t.Id))
        {
            var statusIcon = todo.Status?.ToLowerInvariant() switch
            {
                "completed" => "[x]",
                "in-progress" => "[~]",
                _ => "[ ]"
            };

            sb.AppendLine($"{statusIcon} {todo.Id}. {todo.Title}");
            if (!string.IsNullOrWhiteSpace(todo.Description))
            {
                sb.AppendLine($"    {todo.Description}");
            }
        }

        sb.AppendLine("</todoList>");
        sb.AppendLine();
        sb.AppendLine("Remember to update your todo list as you complete tasks using the ManageTodoList tool.");

        return sb.ToString().TrimEnd();
    }

    #endregion

    #region Private Helpers

    private static string GetOperatingSystemName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "macOS";
        }

        return "Linux";
    }

    /// <summary>
    /// Loads copilot instruction files from codeRefs/ folder and memories/{repo}/.github/ folders.
    /// Separates:
    /// - Full content files: copilot-instructions.md, AGENTS.md (content loaded)
    /// - Metadata-only files: *.instructions.md (only file path + frontmatter parsed)
    /// </summary>
    private async Task<InstructionFilesResult> LoadInstructionFilesAsync(CancellationToken ct)
    {
        var fullContentFiles = new List<InstructionFileContent>();
        var metadataOnlyFiles = new List<InstructionFileMetadata>();
        var sandboxPaths = await _sandboxPaths.GetSandboxPathsAsync();
        var sandboxRoot = sandboxPaths.SandboxRoot;
        var codeRefsPath = sandboxPaths.CodeRefsPath;
        var memoriesPath = sandboxPaths.MemoriesPath;

        // Collect all paths to scan for instruction files
        var pathsToScan = new List<string>();

        // 1. Add codeRefs path if it exists
        if (Directory.Exists(codeRefsPath))
        {
            pathsToScan.Add(codeRefsPath);
        }

        // 2. Add memories/{repo}/.github paths (repo instructions synced from remote)
        if (Directory.Exists(memoriesPath))
        {
            try
            {
                foreach (var repoDir in Directory.GetDirectories(memoriesPath))
                {
                    var githubPath = Path.Combine(repoDir, ".github");
                    if (Directory.Exists(githubPath))
                    {
                        pathsToScan.Add(githubPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Failed to scan memories path for repo instructions: {Path}", memoriesPath);
            }
        }

        if (pathsToScan.Count == 0)
        {
            return new InstructionFilesResult(fullContentFiles, metadataOnlyFiles);
        }

        foreach (var scanPath in pathsToScan)
        {
            try
            {
                // Pattern 1: copilot-instructions.md (full content)
                foreach (var file in Directory.GetFiles(scanPath, "copilot-instructions.md", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(sandboxRoot, file);
                    var content = await File.ReadAllTextAsync(file, ct);
                    fullContentFiles.Add(new InstructionFileContent(relativePath, content));
                }

                // Pattern 2: AGENTS.md (full content)
                foreach (var file in Directory.GetFiles(scanPath, "AGENTS.md", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(sandboxRoot, file);
                    var content = await File.ReadAllTextAsync(file, ct);
                    fullContentFiles.Add(new InstructionFileContent(relativePath, content));
                }

                // Pattern 3: *.instructions.md (metadata only, NOT full content)
                foreach (var file in Directory.GetFiles(scanPath, "*.instructions.md", SearchOption.AllDirectories))
                {
                    // Skip if it's copilot-instructions.md (already handled)
                    if (Path.GetFileName(file).Equals("copilot-instructions.md", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var relativePath = Path.GetRelativePath(sandboxRoot, file);
                    var (description, applyTo) = await ParseInstructionMetadataAsync(file, ct);
                    metadataOnlyFiles.Add(new InstructionFileMetadata(relativePath, description, applyTo));
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Failed to load instruction files from {Path}", scanPath);
            }
        }

        return new InstructionFilesResult(fullContentFiles, metadataOnlyFiles);
    }

    /// <summary>
    /// Parses instruction file frontmatter for description and applyTo fields.
    /// Reads just the first ~50 lines to find YAML frontmatter.
    /// </summary>
    private static async Task<(string? description, string? applyTo)> ParseInstructionMetadataAsync(
        string filePath, CancellationToken ct)
    {
        try
        {
            // Read first 50 lines to find frontmatter
            var lines = new List<string>();
            using var reader = new StreamReader(filePath);
            for (var i = 0; i < 50 && !reader.EndOfStream; i++)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line != null)
                {
                    lines.Add(line);
                }
            }

            // Check for YAML frontmatter (starts and ends with ---)
            if (lines.Count < 2 || lines[0] != "---")
            {
                return (null, null);
            }

            var endIndex = lines.FindIndex(1, l => l == "---");
            if (endIndex < 0)
            {
                return (null, null);
            }

            string? description = null;
            string? applyTo = null;
            for (var i = 1; i < endIndex; i++)
            {
                var line = lines[i];
                if (line.StartsWith("description:"))
                {
                    description = line["description:".Length..].Trim().Trim('"');
                }
                else if (line.StartsWith("applyTo:"))
                {
                    applyTo = line["applyTo:".Length..].Trim().Trim('"');
                }
            }

            return (description, applyTo);
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>
    /// Builds a directory tree string using breadth-first expansion with character limit.
    /// </summary>
    private async Task<string> BuildDirectoryTreeAsync(string rootPath, int maxLength, CancellationToken ct)
    {
        var parts = new List<(string value, string? dirPath, int level)>();

        try
        {
            // Initial population from root
            var rootItems = await GetSortedDirectoryContentsAsync(rootPath, ct);
            var initialParts = ToParts(rootItems, rootPath, level: 0, maxLength);
            parts.AddRange(initialParts);
            var remainingSpace = maxLength - PartsLength(parts);

            // Breadth-first expansion loop
            while (true)
            {
                var didExpand = false;
                var newParts = new List<(string value, string? dirPath, int level)>();

                foreach (var part in parts)
                {
                    if (part.dirPath == null)
                    {
                        // Text part - keep as-is
                        newParts.Add(part);
                    }
                    else
                    {
                        // Directory part - convert to text and expand children
                        newParts.Add((part.value, null, part.level)); // Convert to text

                        var children = await GetSortedDirectoryContentsAsync(part.dirPath, ct);
                        if (ct.IsCancellationRequested)
                        {
                            return string.Empty;
                        }

                        var subParts = ToParts(children, part.dirPath, part.level + 1, remainingSpace - 1);
                        if (subParts.Count > 0)
                        {
                            didExpand = true;
                            remainingSpace -= PartsLength(subParts) + 1; // +1 for newline
                            newParts.AddRange(subParts);
                        }
                    }
                }

                parts = newParts;
                if (!didExpand)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to build directory tree for {Path}", rootPath);
            return $"{Path.GetFileName(rootPath)}/\n    ...";
        }

        return string.Join("\n", parts.Select(p => p.value));
    }

    private static Task<List<(string name, bool isDirectory)>> GetSortedDirectoryContentsAsync(
        string path, CancellationToken ct)
    {
        var entries = new List<(string name, bool isDirectory)>();

        try
        {
            foreach (var file in Directory.GetFiles(path))
            {
                var name = Path.GetFileName(file);
                if (!name.StartsWith('.') && !ShouldExcludeFile(name))
                {
                    entries.Add((name, false));
                }
            }

            foreach (var dir in Directory.GetDirectories(path))
            {
                var name = Path.GetFileName(dir);
                if (!name.StartsWith('.') && !ShouldExcludeFolder(name))
                {
                    entries.Add((name, true));
                }
            }
        }
        catch
        {
            return Task.FromResult(entries);
        }

        // Sort: files first (alphabetically), then directories (alphabetically)
        entries.Sort((a, b) =>
        {
            if (a.isDirectory == b.isDirectory)
            {
                return string.Compare(a.name, b.name, StringComparison.Ordinal);
            }

            return a.isDirectory ? 1 : -1; // Files before directories
        });

        return Task.FromResult(entries);
    }

    private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        "jpg", "jpeg", "jpe", "png", "gif", "bmp", "tif", "tiff", "tga", "ico", "icns", "xpm",
        "webp", "svg", "eps", "heif", "heic", "raw", "arw", "cr2", "cr3", "nef", "dng",
        // Video
        "mp4", "m4v", "mkv", "webm", "mov", "avi", "wmv", "flv",
        // Audio
        "mp3", "wav", "m4a", "flac", "ogg", "wma", "weba", "aac", "pcm",
        // Compressed
        "7z", "bz2", "gz", "tgz", "rar", "tar", "xz", "zip", "vsix", "iso", "img", "pkg",
        // Fonts
        "woff", "woff2", "otf", "ttf", "eot",
        // 3D Models
        "obj", "fbx", "stl", "3ds", "dae", "blend", "ply", "glb", "gltf", "max", "c4d",
        // Docs / Office
        "pdf", "ai", "ps", "indd", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "odt", "ods", "odp", "rtf", "psd", "pbix",
        // Binaries / Compiled / Serialized
        "exe", "dll", "dylib", "so", "a", "o", "lib", "out", "elf", "wasm", "pdb", "idb", "sym",
        "nupkg", "winmd", "pyc", "pkl", "pickle", "pyd", "rlib", "rmeta", "dill",
        "jar", "class", "ear", "war", "apk", "dex", "phar",
        // Misc binary / db / caches
        "temp", "tmp", "db", "sqlite", "parquet", "bin", "dat", "data", "hex", "cache", "sum", "hash",
        "coverage", "testlog", "log", "trace", "tlog", "snap", "msi", "deb",
        "vsidx", "suo", "xcuserstate", "download", "map", "tsbuildinfo", "jsbundle",
        "lock", "git", "pack"
    };

    private static readonly HashSet<string> ExcludedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "venv", "out", "dist", ".git", ".yarn", ".npm", ".venv", ".vscode-test"
    };

    private static readonly HashSet<string> ExcludedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ds_store", "thumbs.db", "package-lock.json", "yarn.lock", ".cache"
    };

    private static bool ShouldExcludeFile(string fileName)
    {
        if (ExcludedFiles.Contains(fileName))
        {
            return true;
        }

        var ext = Path.GetExtension(fileName).TrimStart('.');
        return ExcludedExtensions.Contains(ext);
    }

    private static bool ShouldExcludeFolder(string folderName)
    {
        return ExcludedFolders.Contains(folderName);
    }

    private static List<(string value, string? dirPath, int level)> ToParts(
        List<(string name, bool isDirectory)> items,
        string parentPath,
        int level,
        int maxLength)
    {
        var indent = new string('\t', level);
        var parts = new List<(string value, string? dirPath, int level)>();
        var remainingSpace = maxLength;

        for (var i = 0; i < items.Count; i++)
        {
            var (name, isDir) = items[i];
            var str = indent + name + (isDir ? "/" : "");

            if (str.Length > remainingSpace)
            {
                // Try adding "..." placeholder
                var placeholder = indent + "...";

                // Remove previous items until there's space for placeholder
                while (placeholder.Length > remainingSpace && parts.Count > 0)
                {
                    remainingSpace += parts[^1].value.Length + 1;
                    parts.RemoveAt(parts.Count - 1);
                }

                if (placeholder.Length <= remainingSpace)
                {
                    parts.Add((placeholder, null, level));
                }

                break;
            }

            if (isDir)
            {
                parts.Add((str, Path.Combine(parentPath, name), level));
            }
            else
            {
                parts.Add((str, null, level));
            }

            remainingSpace -= str.Length;
            if (i != items.Count - 1)
            {
                remainingSpace -= 1; // Account for newline
            }
        }

        return parts;
    }

    private static int PartsLength(List<(string value, string? dirPath, int level)> parts)
    {
        var len = parts.Sum(p => p.value.Length);
        return len + Math.Max(0, parts.Count - 1); // Add newlines between parts
    }

    /// <summary>
    /// Discovers git repositories in the codeRefs/ folder and extracts branch info.
    /// Parses .git folder directly (no git CLI dependency).
    /// </summary>
    private async Task<List<GitRepositoryInfo>> GetGitRepositoriesAsync(string sandboxRoot, CancellationToken ct)
    {
        var result = new List<GitRepositoryInfo>();
        var codeRefsPath = Path.Combine(sandboxRoot, "codeRefs");

        if (!Directory.Exists(codeRefsPath))
        {
            return result;
        }

        try
        {
            // Find all .git directories (indicating repo roots)
            var gitDirs = Directory.GetDirectories(codeRefsPath, ".git", SearchOption.AllDirectories);

            foreach (var gitDir in gitDirs)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var repoRoot = Path.GetDirectoryName(gitDir)!;
                    var repoName = Path.GetFileName(repoRoot);

                    // Get current branch by parsing .git/HEAD
                    var currentBranch = await ParseCurrentBranchAsync(gitDir, ct);

                    // Get default branch and owner from .git/config
                    var (owner, defaultBranch) = await ParseGitConfigAsync(gitDir, ct);

                    result.Add(new GitRepositoryInfo(repoName, owner, currentBranch, defaultBranch));
                }
                catch
                {
                    // Skip corrupted/unreadable .git folders
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to discover git repositories in {Path}", codeRefsPath);
        }

        return result;
    }

    /// <summary>
    /// Parses current branch from .git/HEAD file.
    /// </summary>
    private static async Task<string> ParseCurrentBranchAsync(string gitDir, CancellationToken ct)
    {
        var headPath = Path.Combine(gitDir, "HEAD");
        if (!File.Exists(headPath))
        {
            return "unknown";
        }

        try
        {
            var content = (await File.ReadAllTextAsync(headPath, ct)).Trim();

            // Check if it's a ref (normal branch)
            if (content.StartsWith("ref: refs/heads/"))
            {
                return content["ref: refs/heads/".Length..];
            }

            // Detached HEAD (commit hash) - return abbreviated hash
            if (content.Length >= 7)
            {
                return content[..7];
            }

            return "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Parses .git/config to extract remote origin URL and default branch.
    /// </summary>
    private static async Task<(string owner, string defaultBranch)> ParseGitConfigAsync(
        string gitDir, CancellationToken ct)
    {
        var configPath = Path.Combine(gitDir, "config");
        var owner = "unknown";
        var defaultBranch = "main"; // Default assumption

        if (!File.Exists(configPath))
        {
            return (owner, defaultBranch);
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(configPath, ct);
            var inRemoteOrigin = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed == "[remote \"origin\"]")
                {
                    inRemoteOrigin = true;
                    continue;
                }

                if (trimmed.StartsWith('[') && inRemoteOrigin)
                {
                    inRemoteOrigin = false;
                    continue;
                }

                if (inRemoteOrigin && trimmed.StartsWith("url = "))
                {
                    var url = trimmed["url = ".Length..];
                    owner = ExtractOwnerFromUrl(url);
                }
            }

            // Try to read default branch from refs/remotes/origin/HEAD
            var originHeadPath = Path.Combine(gitDir, "refs", "remotes", "origin", "HEAD");
            if (File.Exists(originHeadPath))
            {
                var content = (await File.ReadAllTextAsync(originHeadPath, ct)).Trim();
                if (content.StartsWith("ref: refs/remotes/origin/"))
                {
                    defaultBranch = content["ref: refs/remotes/origin/".Length..];
                }
            }
            else
            {
                // Fallback: check if main or master refs exist
                var refsPath = Path.Combine(gitDir, "refs", "remotes", "origin");
                if (Directory.Exists(refsPath))
                {
                    if (File.Exists(Path.Combine(refsPath, "main")))
                    {
                        defaultBranch = "main";
                    }
                    else if (File.Exists(Path.Combine(refsPath, "master")))
                    {
                        defaultBranch = "master";
                    }
                }
            }
        }
        catch
        {
            // Return defaults on any error
        }

        return (owner, defaultBranch);
    }

    /// <summary>
    /// Extracts owner from git remote URL.
    /// </summary>
    private static string ExtractOwnerFromUrl(string url)
    {
        try
        {
            // HTTPS format: https://github.com/owner/repo.git
            var httpsMatch = Regex.Match(url, @"github\.com/([^/]+)/");
            if (httpsMatch.Success)
            {
                return httpsMatch.Groups[1].Value;
            }

            // SSH format: git@github.com:owner/repo.git
            var sshMatch = Regex.Match(url, @"github\.com:([^/]+)/");
            if (sshMatch.Success)
            {
                return sshMatch.Groups[1].Value;
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return "unknown";
    }

    #endregion
}
