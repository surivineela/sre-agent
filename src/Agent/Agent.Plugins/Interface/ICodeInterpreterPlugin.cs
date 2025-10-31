// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

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
    /// Execute an arbitrary (but sandbox‑validated) python snippet and return stdout / stderr summary.
    /// </summary>
    Task<string> ExecutePythonSnippetAsync(string pythonCode, int timeoutSeconds);

    /// <summary>
    /// Execute python that produces a PDF file; the file is copied back internally and persisted locally.
    /// Returns a status message with a downloadable relative link (no base64 content is returned to the user).
    /// </summary>
    Task<string> GeneratePdfReportAsync(string pythonCode, string expectedOutputFilename, string saveAsFilename, int timeoutSeconds);

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
}
