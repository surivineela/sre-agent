// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Models.WorkspaceTools;

namespace Agent.Plugins.Interface;

/// <summary>
/// Interface for VS Code-like agent tools implementation.
/// All methods return Task&lt;string&gt; - on success return content directly, on failure return "Tool call failed: {reason}".
/// </summary>
public interface IWorkspaceToolsPlugin
{
    /// <summary>
    /// Gets whether VS Code tools are enabled.
    /// When false, tools and ambient context injection are disabled.
    /// </summary>
    bool Enabled { get; }

    #region State Accessors
    /// <summary>
    /// Gets the todo list for the current thread.
    /// </summary>
    IReadOnlyList<WorkspaceTodoItem>? GetTodoList();

    /// <summary>
    /// Gets terminal state formatted for context injection.
    /// </summary>
    string GetTerminalStateForContext();

    #endregion

    #region File Operations

    /// <summary>
    /// Read the contents of a file with line range (1-indexed).
    /// </summary>
    /// <param name="filePath">The absolute path of the file to read.</param>
    /// <param name="startLine">The line number to start reading from, 1-based.</param>
    /// <param name="endLine">The inclusive line number to end reading at, 1-based.</param>
    /// <returns>File content with metadata, or error message.</returns>
    Task<string> ReadFileAsync(string filePath, int startLine, int endLine);

    /// <summary>
    /// Create a new file with the specified content. Directory will be created if needed.
    /// </summary>
    /// <param name="filePath">The absolute path to the file to create.</param>
    /// <param name="content">The content to write to the file.</param>
    /// <returns>Success message or error.</returns>
    Task<string> CreateFileAsync(string filePath, string content);

    /// <summary>
    /// Create a new directory structure recursively (like mkdir -p).
    /// </summary>
    /// <param name="dirPath">The absolute path to the directory to create.</param>
    /// <returns>Success message or error.</returns>
    Task<string> CreateDirectoryAsync(string dirPath);

    /// <summary>
    /// List the contents of a directory.
    /// </summary>
    /// <param name="path">The absolute path to the directory to list.</param>
    /// <returns>Directory listing or error.</returns>
    Task<string> ListDirectoryAsync(string path);

    #endregion

    #region Edit Operations

    /// <summary>
    /// Find and replace exact string in file.
    /// </summary>
    /// <param name="filePath">The absolute path to the file to edit.</param>
    /// <param name="oldString">The exact literal text to replace.</param>
    /// <param name="newString">The exact literal text to replace oldString with.</param>
    /// <returns>Success message or error.</returns>
    Task<string> ReplaceStringInFileAsync(string filePath, string oldString, string newString);

    /// <summary>
    /// Apply multiple replacement operations across files.
    /// </summary>
    /// <param name="explanation">Brief explanation of what the multi-replace operation accomplishes.</param>
    /// <param name="replacements">Array of replacement operations to apply sequentially.</param>
    /// <returns>Summary of successes and failures.</returns>
    Task<string> MultiReplaceStringInFileAsync(string explanation, ReplaceOperation[] replacements);

    #endregion

    #region Search Operations

    /// <summary>
    /// Search for files by glob pattern.
    /// </summary>
    /// <param name="query">Glob pattern to match files.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <returns>Matching file paths or error.</returns>
    Task<string> FileSearchAsync(string query, int? maxResults = null);

    /// <summary>
    /// Text/regex search in files with optional filters.
    /// </summary>
    /// <param name="query">The pattern to search for.</param>
    /// <param name="isRegexp">Whether the pattern is a regex.</param>
    /// <param name="includePattern">Glob pattern to filter files to search.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="includeIgnoredFiles">Whether to include files normally ignored by .gitignore.</param>
    /// <returns>Search results or error.</returns>
    Task<string> GrepSearchAsync(string query, bool isRegexp, string? includePattern = null,
                                  int? maxResults = null, bool includeIgnoredFiles = false);

    #endregion

    #region Terminal Operations

    /// <summary>
    /// Execute a shell command in a terminal session.
    /// </summary>
    /// <param name="command">The command to run.</param>
    /// <param name="explanation">A one-sentence description of what the command does.</param>
    /// <param name="isBackground">Whether the command starts a background process.</param>
    /// <returns>Command output with exit code for foreground, or PID and output file for background commands.</returns>
    Task<string> RunInTerminalAsync(string command, string explanation, bool isBackground);

    /// <summary>
    /// Get the last command run in the active terminal.
    /// </summary>
    /// <returns>Last command or error if no terminal exists.</returns>
    Task<string> GetTerminalLastCommandAsync();

    /// <summary>
    /// Get the output of a background task started by RunInTerminalAsync.
    /// </summary>
    /// <param name="taskId">The task ID from previous output of execute bash.</param>
    /// <param name="block">Whether to wait for completion.</param>
    /// <param name="timeout">Max wait time in milliseconds.</param>
    /// <returns>Task output or error.</returns>
    Task<string> GetBackgroundTaskOutputAsync(string taskId, bool block = true, int timeout = 30000);

    #endregion

    #region Task Management

    /// <summary>
    /// Manage a structured todo list to track progress.
    /// </summary>
    /// <param name="operation">"read" to get current list, "write" to replace entire list.</param>
    /// <param name="todoList">Array of todo items (required for write operation).</param>
    /// <returns>Todo list in markdown format or confirmation message.</returns>
    Task<string> ManageTodoListAsync(string operation, WorkspaceTodoItem[]? todoList = null);

    #endregion

    #region Web Operations

    /// <summary>
    /// Fetch and extract main content from web pages.
    /// </summary>
    /// <param name="urls">Array of URLs to fetch content from.</param>
    /// <param name="query">The query to search for in the web page's content.</param>
    /// <returns>Extracted content or error.</returns>
    Task<string> FetchWebpageAsync(string[] urls, string query);

    #endregion
}
