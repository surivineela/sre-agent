// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Common.Services;
using Agent.Logging;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Hooks;

/// <summary>
/// Implementation of quiet file operations for hook executors.
/// Uses LocalFileTools internally without communication service calls.
/// </summary>
public class HookFileTools : IHookFileTools
{
    private readonly ISandboxPaths _sandboxPaths;
    private readonly ILogger<HookFileTools> _logger;
    private LocalFileTools? _localFileTools;
    private string? _hooksDirectory;

    public HookFileTools(ISandboxPaths sandboxPaths, ILogger<HookFileTools> logger)
    {
        _sandboxPaths = sandboxPaths ?? throw new ArgumentNullException(nameof(sandboxPaths));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region LLM Tool Methods

    /// <summary>
    /// ReadFile tool for hook LLM calls. Use AIFunctionFactory.Create(hookFileTools.ReadFile) to create the tool.
    /// </summary>
    [Description("Read the contents of a file.\n\nYou must specify the line range you're interested in. Line numbers are 1-indexed. " +
        "If the file contents returned are insufficient for your task, you may call this tool again to retrieve more content. " +
        "Prefer reading larger ranges over doing many small reads. The execution_summary field contains the path to the transcript file.")]
    public async Task<string> ReadFile(
        [Description("The absolute path of the file to read.")] string filePath,
        [Description("The line number to start reading from (1-based).")] int startLine,
        [Description("The inclusive line number to end reading at (1-based).")] int endLine)
    {
        return await ReadFileQuietAsync(filePath, startLine, endLine);
    }

    /// <summary>
    /// GrepSearch tool for hook LLM calls. Use AIFunctionFactory.Create(hookFileTools.GrepSearch) to create the tool.
    /// </summary>
    [Description("Do a fast text search in files. Use this when you want to search for an exact string or regex pattern. " +
        "Use regex patterns with alternation (|) or character classes to search for multiple words at once. " +
        "For example, use 'error|warning|failed' to look for all of those words simultaneously. " +
        "This is useful for searching through the execution transcript to find specific events or patterns.")]
    public async Task<string> GrepSearch(
        [Description("The pattern to search for in file contents. Use regex with alternation (e.g., 'word1|word2|word3') " +
            "or character classes to find multiple patterns. Set isRegexp=true for regex patterns. Case-insensitive.")] string query,
        [Description("Whether the pattern should be interpreted as a regular expression.")] bool isRegexp,
        [Description("Optional glob pattern to filter which files to search. Example: '*.txt' or 'transcript*.txt'.")] string? includePattern = null,
        [Description("Maximum number of results to return. Defaults to 20 if not specified.")] int? maxResults = null)
    {
        return await GrepSearchQuietAsync(query, isRegexp, includePattern, maxResults);
    }

    #endregion

    #region Internal Implementation Methods

    /// <inheritdoc />
    public async Task<string> ReadFileQuietAsync(string filePath, int startLine, int endLine)
    {
        var fileTools = await GetFileToolsAsync();
        return await fileTools.ReadFileAsync(filePath, startLine, endLine);
    }

    /// <inheritdoc />
    public async Task<string> GrepSearchQuietAsync(string query, bool isRegexp, string? includePattern = null, int? maxResults = null)
    {
        var fileTools = await GetFileToolsAsync();
        return await fileTools.GrepSearchAsync(query, isRegexp, includePattern, maxResults);
    }

    /// <inheritdoc />
    public async Task<string> SaveTranscriptAsync(Guid threadId, string content)
    {
        var hooksDir = await EnsureHooksDirectoryAsync();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var filename = $"transcript_{threadId}_{timestamp}.txt";
        var filePath = Path.Combine(hooksDir, filename);

        // Normalize line endings for consistent cross-platform behavior
        var normalizedContent = LocalFileTools.NormalizeLineEndings(content);

        await File.WriteAllTextAsync(filePath, normalizedContent);
        _logger.LogInternalDebug("Saved transcript to {FilePath} ({Length} chars)", filePath, normalizedContent.Length);

        return filePath;
    }

    /// <inheritdoc />
    public async Task DeleteTranscriptAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        try
        {
            // Validate path is within hooks directory before deleting
            var hooksDir = await EnsureHooksDirectoryAsync();
            var fullPath = Path.GetFullPath(filePath);

            if (!fullPath.StartsWith(hooksDir, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInternalWarning("Attempted to delete file outside hooks directory: {FilePath}", filePath);
                return;
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInternalDebug("Deleted transcript file {FilePath}", fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to delete transcript file {FilePath}", filePath);
        }
    }

    #endregion

    #region Private Helpers

    private async Task<LocalFileTools> GetFileToolsAsync()
    {
        if (_localFileTools != null)
        {
            return _localFileTools;
        }

        var sandboxPaths = await _sandboxPaths.GetSandboxPathsAsync();
        _localFileTools = new LocalFileTools(_logger, sandboxPaths.SandboxRoot);
        return _localFileTools;
    }

    private async Task<string> EnsureHooksDirectoryAsync()
    {
        if (_hooksDirectory != null)
        {
            return _hooksDirectory;
        }

        var sandboxPaths = await _sandboxPaths.GetSandboxPathsAsync();
        _hooksDirectory = Path.Combine(sandboxPaths.TmpPath, "hooks");

        if (!Directory.Exists(_hooksDirectory))
        {
            Directory.CreateDirectory(_hooksDirectory);
            _logger.LogInternalDebug("Created hooks directory at {HooksDirectory}", _hooksDirectory);
        }

        return _hooksDirectory;
    }

    #endregion
}
