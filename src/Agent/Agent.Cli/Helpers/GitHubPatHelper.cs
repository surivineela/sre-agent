// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;

namespace Agent.Cli.Helpers;

/// <summary>
/// Helper class for obtaining GitHub Personal Access Tokens using GitHub CLI.
/// </summary>
public static class GitHubPatHelper
{
    /// <summary>
    /// Result of a PAT retrieval operation.
    /// </summary>
    public record PatResult(bool Success, string? Token, string? ErrorMessage);

    /// <summary>
    /// Gets a PAT from the GitHub CLI (gh auth token).
    /// This uses the user's existing gh CLI authentication.
    /// </summary>
    /// <returns>The result of the PAT retrieval.</returns>
    public static async Task<PatResult> GetPatAsync()
    {
        try
        {
            // Check if gh CLI is available
            var ghCheckResult = await ExecuteCommandAsync("gh", "--version");
            if (ghCheckResult.ExitCode != 0)
            {
                return new PatResult(false, null,
                    "GitHub CLI (gh) is not installed or not in PATH. Install from https://cli.github.com and run 'gh auth login'");
            }

            // Get token from gh CLI (silent, no prompts - uses existing login)
            DebugLogger.Debug("PAT", "Getting PAT from GitHub CLI...");
            var tokenResult = await ExecuteCommandAsync("gh", "auth token");

            if (tokenResult.ExitCode != 0)
            {
                var errorMsg = !string.IsNullOrWhiteSpace(tokenResult.Error) ? tokenResult.Error : tokenResult.Output;
                return new PatResult(false, null,
                    $"Not logged in to GitHub CLI. Run 'gh auth login' first. Error: {errorMsg}");
            }

            var token = tokenResult.Output?.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                return new PatResult(false, null, "GitHub CLI returned empty token");
            }

            return new PatResult(true, token, null);
        }
        catch (Exception ex)
        {
            return new PatResult(false, null, $"Failed to get GitHub PAT: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes a command and returns the result.
    /// On Windows, uses cmd.exe to properly resolve commands like 'gh' which may be batch scripts.
    /// </summary>
    private static async Task<(int ExitCode, string Output, string Error)> ExecuteCommandAsync(string command, string arguments)
    {
        try
        {
            string fileName;
            string fullArguments;

            // On Windows, gh might be a batch script, so run through cmd.exe
            if (OperatingSystem.IsWindows())
            {
                fileName = "cmd.exe";
                fullArguments = $"/c {command} {arguments}";
            }
            else
            {
                fileName = command;
                fullArguments = arguments;
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = fullArguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                return (-1, string.Empty, "Failed to start process");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return (process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            return (-1, string.Empty, $"Exception executing command: {ex.Message}");
        }
    }
}
