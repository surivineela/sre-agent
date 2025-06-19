using System;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Agent.Logging;

namespace Agent.Core.Services;

public class ExternalProcessCommand
{
    private ILogger _logger;
    private readonly string _exe;
    private readonly string[] _arguments;
    private readonly string? _stdin;
    private readonly TimeSpan _timeout;
    private readonly IDictionary<string, string> _envs;

    public ExternalProcessCommand(ILogger logger, string exe, string[] arguments, string? stdin = null, TimeSpan? timeout = null, IDictionary<string, string>? envs = null)
    {
        _logger = logger;
        _exe = exe;
        _arguments = arguments;
        _stdin = stdin;
        _timeout = timeout ?? TimeSpan.FromMinutes(1);
        _envs = envs ?? new Dictionary<string, string>();
    }

    public async Task<string> ExecuteAsync(CancellationToken cancellationToken)
    {
        var exePath = GetAbsoluteExecutablePath(_exe);
        var processInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = string.Join(" ", _arguments),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = !string.IsNullOrEmpty(_stdin),
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var env in _envs)
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
        if (!string.IsNullOrEmpty(_stdin))
        {
            await process.StandardInput.WriteAsync(_stdin);
            process.StandardInput.Close();
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait for completion with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);
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
            _logger.LogInternalError($"Process '{_exe} {string.Join(" ", _arguments)}' failed with exit code {process.ExitCode}: {errorMessage}");
            throw new InvalidOperationException($"Process failed with exit code {process.ExitCode}: {errorMessage}");
        }

        return outputBuilder.ToString();
    }

    private static string GetAbsoluteExecutablePath(string exe)
    {
        if (Path.IsPathRooted(exe))
        {
            return exe;
        }

        string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string[] paths = pathEnv.Split(Path.PathSeparator);
        foreach (string path in paths)
        {
            string fullPath = Path.Combine(path, exe);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return exe;
    }
}
