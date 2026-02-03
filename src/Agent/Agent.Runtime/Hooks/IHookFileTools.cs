// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Runtime.Hooks;

/// <summary>
/// Interface for quiet file operations used by hook executors.
/// These operations do not send messages to the communication service,
/// making them suitable for hook execution contexts.
/// </summary>
public interface IHookFileTools
{
    /// <summary>
    /// Reads file contents without streaming to communication service.
    /// </summary>
    /// <param name="filePath">The absolute path to the file to read.</param>
    /// <param name="startLine">The 1-based line number to start reading from.</param>
    /// <param name="endLine">The 1-based line number to end reading at (inclusive).</param>
    /// <returns>Formatted string with file content.</returns>
    Task<string> ReadFileQuietAsync(string filePath, int startLine, int endLine);

    /// <summary>
    /// Performs a grep search without streaming to communication service.
    /// </summary>
    /// <param name="query">The search pattern.</param>
    /// <param name="isRegexp">Whether the pattern is a regular expression.</param>
    /// <param name="includePattern">Optional glob pattern to filter files.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <returns>JSON-serialized search results.</returns>
    Task<string> GrepSearchQuietAsync(string query, bool isRegexp, string? includePattern = null, int? maxResults = null);

    /// <summary>
    /// Saves transcript content to a temporary file for hook access.
    /// </summary>
    /// <param name="threadId">The thread ID for filename generation.</param>
    /// <param name="content">The transcript content to save.</param>
    /// <returns>The full path to the saved transcript file.</returns>
    Task<string> SaveTranscriptAsync(Guid threadId, string content);

    /// <summary>
    /// Deletes a transcript file created by SaveTranscriptAsync.
    /// </summary>
    /// <param name="filePath">The path to the transcript file to delete.</param>
    Task DeleteTranscriptAsync(string filePath);
}
