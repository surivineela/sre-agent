// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Core.Models.Session;

namespace Agent.Core.Interfaces;

public interface ISessionPoolService
{
    string BuildSessionIdentifier(string? agentName = null, string? threadId = null, bool randomSuffix = true);
    Task<(int, string, string)> ExecuteCliLegacyAsync(string command, string accessToken, string identifier);
    Task<(int, string, string)> ExecuteCliAsync(string command, string identifier, Dictionary<string, string>? tokens);

    /// <summary>
    /// Execute a bash command within the dedicated Code Interpreter session pool (isolated ACA environment for Python/report generation).
    /// Intended for internal orchestration (e.g., writing & running Python scripts). The command runs via /bin/bash -c.
    /// <para>Notes:</para>
    ///  - Provides an isolated pool separate from the general session pool.
    ///  - Not exposed directly as a user tool; higher-level plugins (e.g., CodeInterpreterPlugin) build safe commands.
    ///  - <paramref name="timeoutSeconds"/> caps total execution time (clamped 5-900 seconds).
    /// Returns raw session execution metadata (exit code, stdout, stderr).
    /// </summary>
    Task<SessionResponse> ExecuteShellCommandInCodeInterpreterPoolAsync(string command, string identifier, int timeoutSeconds);

    /// <summary>
    /// Execute inline Python code directly via the code interpreter pool /code/execute endpoint.
    /// Returns parsed stdout/stderr when available plus raw JSON.
    /// </summary>
    Task<CodeExecutionResponse> ExecutePythonInlineAsync(string code, string identifier, int timeoutSeconds);

    /// <summary>
    /// Download a file from the active code interpreter session's /mnt/data directory.
    /// </summary>
    Task<byte[]> DownloadSessionFileAsync(string identifier, string filename);

    /// <summary>
    /// List all files in the active code interpreter session's /mnt/data directory.
    /// Returns metadata for each file (name, size, etc.).
    /// </summary>
    Task<string> ListSessionFilesAsync(string identifier);

    /// <summary>
    /// Upload a file to the active code interpreter session.
    /// </summary>
    /// <param name="identifier">Session identifier</param>
    /// <param name="filename">Name of the file to upload</param>
    /// <param name="fileContent">Binary content of the file</param>
    /// <param name="destinationPath">Optional destination path within the session</param>
    Task UploadSessionFileAsync(string identifier, string filename, byte[] fileContent, string? destinationPath = null);
}
