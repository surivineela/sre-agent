using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Helpers;

public class ExecuteCommandHelper
{
    public static async Task<string> ExecuteCommand(string command, string[] arguments, Dictionary<string, string>? envs = null)
    {
        return await ExecuteCommand(command, null, arguments, envs);
    }

    public static async Task<string> ExecuteCommand(string command, string? stdin, string[] arguments, Dictionary<string, string>? envs = null)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
            Arguments = OperatingSystem.IsWindows()
            ? $"/c {command} {string.Join(" ", arguments)}"
            : $"-c \"{command} {string.Join(" ", arguments)}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = !string.IsNullOrEmpty(stdin),
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var env in envs ?? new Dictionary<string, string>())
        {
            processInfo.Environment[env.Key] = env.Value;
        }

        using var process = new Process { StartInfo = processInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        // Set up data received handlers
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        // Start the process
        process.Start();

        // Write stdin if provided
        if (!string.IsNullOrEmpty(stdin))
        {
            await process.StandardInput.WriteAsync(stdin);
            process.StandardInput.Close();
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait for completion with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Kill the process if it times out
            try
            {
                process.Kill();
            }
            catch { }

            return "[Unexpected Exception]: command execution timed out after 1 minute.";
        }

        // Check for errors
        if (process.ExitCode != 0)
        {
            var errorMessage = errorBuilder.ToString();
            throw new InvalidOperationException($"Command '{command} {string.Join(" ", arguments)}' failed with exit code {process.ExitCode}: {errorMessage}");
        }

        return outputBuilder.ToString();
    }
}
