using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Helpers;
public class ExecuteCommandHelper
{
    public static async Task<string> ExecuteCommand(string command, params string[] arguments)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
            Arguments = OperatingSystem.IsWindows()
            ? $"/c {command} {string.Join(" ", arguments)}"
            : $"-c \"{command} {string.Join(" ", arguments)}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

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
