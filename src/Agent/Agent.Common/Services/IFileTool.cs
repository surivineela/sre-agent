// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Common.ApiModels;

namespace Agent.Common.Services;

/// <summary>
/// Interface for file system operations in workspace tools.
/// Provides abstraction for both local and remote file operations.
/// </summary>
public interface IFileTool
{
    /// <summary>
    /// Reads a file with optional line range (1-indexed).
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <param name="startLine">1-based starting line number.</param>
    /// <param name="endLine">1-based ending line number.</param>
    /// <returns>Formatted string with file content or error message.</returns>
    Task<string> ReadFileAsync(string filePath, int startLine, int endLine);

    /// <summary>
    /// Creates a new file with the specified content.
    /// </summary>
    /// <param name="filePath">Path to the file to create.</param>
    /// <param name="content">Content to write to the file.</param>
    /// <returns>Success or error message.</returns>
    Task<string> CreateFileAsync(string filePath, string content);

    /// <summary>
    /// Creates a directory (including parent directories if needed).
    /// </summary>
    /// <param name="dirPath">Path to the directory to create.</param>
    /// <returns>Success or error message.</returns>
    Task<string> CreateDirectoryAsync(string dirPath);

    /// <summary>
    /// Lists the contents of a directory.
    /// </summary>
    /// <param name="path">Path to the directory.</param>
    /// <returns>Directory listing or error message.</returns>
    Task<string> ListDirectoryAsync(string path);

    /// <summary>
    /// Replaces a string in a file.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <param name="oldString">The string to replace.</param>
    /// <param name="newString">The replacement string.</param>
    /// <returns>Success or error message.</returns>
    Task<string> ReplaceStringInFileAsync(string filePath, string oldString, string newString);

    /// <summary>
    /// Performs multiple replacement operations across files.
    /// </summary>
    /// <param name="explanation">Overall explanation for the operation.</param>
    /// <param name="replacements">Array of replacement operations to perform.</param>
    /// <returns>Summary of successful and failed replacements.</returns>
    Task<string> MultiReplaceStringInFileAsync(string explanation, ReplaceOperation[] replacements);

    /// <summary>
    /// Searches for files matching a glob pattern.
    /// </summary>
    /// <param name="query">The glob pattern to search for.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <returns>Search results or error message.</returns>
    Task<string> FileSearchAsync(string query, int? maxResults = null);

    /// <summary>
    /// Performs a grep search in files.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="isRegexp">Whether the query is a regular expression.</param>
    /// <param name="includePattern">Optional glob pattern to filter files.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="includeIgnoredFiles">Whether to include normally ignored files.</param>
    /// <returns>Search results or error message.</returns>
    Task<string> GrepSearchAsync(string query, bool isRegexp, string? includePattern = null,
                                  int? maxResults = null, bool includeIgnoredFiles = false);
}
