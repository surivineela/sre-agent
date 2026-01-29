// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Core.Models.Api.v1;

namespace Agent.Plugins.Interface;

/// <summary>
/// Interface for executing constrained Python code ("code interpreter") inside an ACA Sessions pool.
/// Intended for safe report / artifact generation (e.g. PDF) with strict egress and import limitations.
/// </summary>
public interface ICodeInterpreterPlugin
{
    Guid? ThreadId { get; set; }

    /// <summary>
    /// Execute an arbitrary (but sandbox‑validated) python code and return the execution response.
    /// The response contains stdout, stderr, result, and any auto-retrieved files.
    /// </summary>
    Task<CodeExecutionResponse> ExecutePythonCodeAsync(string pythonCode, int timeoutSeconds);

    /// <summary>
    /// Execute a constrained POSIX shell command (bash) within the code interpreter sandbox.
    /// Ensures execution occurs from /mnt/data and enforces timeout/background policies.
    /// </summary>
    Task<string> ExecuteShellCommandAsync(string command, string explanation, bool isBackground, int timeoutSeconds);

    /// <summary>
    /// Execute python that produces a PDF file; the file is copied back internally and persisted locally.
    /// Returns a status message with a downloadable relative link (no base64 content is returned to the user).
    /// </summary>
    Task<string> GeneratePdfReportAsync(string pythonCode, string expectedOutputFilename, string saveAsFilename, int timeoutSeconds);

    /// <summary>
    /// Read text content from a file stored under /mnt/data in the current session, with simple paging support.
    /// </summary>
    Task<string> ReadSessionFileAsync(string relativePath, int offset, int limit);

    /// <summary>
    /// List all files in the /mnt/data directory of the current code interpreter session.
    /// Returns a JSON array of file metadata (name, size, modified timestamp).
    /// </summary>
    Task<string> ListSessionFilesAsync();

    /// <summary>
    /// Download a file from the session's /mnt/data directory and save it locally.
    /// Supports multiple file types: images (PNG, JPG, GIF, SVG, WebP), data files (CSV, Excel, JSON, TXT),
    /// documents (PDF, HTML, Markdown), and configuration files (YAML, XML).
    /// Returns a download link and renders images inline when applicable.
    /// </summary>
    Task<string> GetSessionFileAsync(string filename, string saveAsFilename);

    /// <summary>
    /// Search for text within files under /mnt/data using grep-like semantics with optional glob filtering.
    /// </summary>
    Task<string> GrepSessionFilesAsync(string query, bool isRegexp, string includePattern, int maxResults, int timeoutSeconds);

    /// <summary>
    /// Upload a file to the session's /mnt/data directory using a tool output file key.
    /// Retrieves the file from tool output storage and uploads it to the session pool.
    /// </summary>
    Task<string> UploadFileToSessionAsync(string fileKey);
}
