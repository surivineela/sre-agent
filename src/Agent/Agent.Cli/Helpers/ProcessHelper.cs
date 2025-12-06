// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;

namespace Agent.Cli.Helpers;

/// <summary>
/// Helper class for executing external processes.
/// </summary>
public static class ProcessHelper
{
    /// <summary>
    /// Executes a PowerShell script and returns the result.
    /// </summary>
    /// <param name="scriptPath">Absolute path to the PowerShell script file.</param>
    /// <param name="arguments">Arguments to pass to the script.</param>
    /// <param name="workingDirectory">Working directory for the process.</param>
    /// <returns>A tuple containing (exitCode, standardOutput, standardError).</returns>
    public static async Task<(int ExitCode, string Output, string Error)> ExecutePowerShellScriptAsync(
        string scriptPath,
        string arguments,
        string? workingDirectory = null)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "pwsh.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
        };

        return await ExecuteProcessAsync(processStartInfo);
    }

    /// <summary>
    /// Executes a process with the given start info.
    /// </summary>
    /// <param name="processStartInfo">Process start information.</param>
    /// <returns>A tuple containing (exitCode, standardOutput, standardError).</returns>
    public static async Task<(int ExitCode, string Output, string Error)> ExecuteProcessAsync(ProcessStartInfo processStartInfo)
    {
        try
        {
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
            return (-1, string.Empty, $"Exception executing process: {ex.Message}");
        }
    }
}
