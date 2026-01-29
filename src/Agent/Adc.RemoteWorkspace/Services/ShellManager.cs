using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Agent.Adc.RemoteWorkspace.Services;

public class ShellManager : IDisposable
{
    private readonly Process _process;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<string, BackgroundJob> _backgroundJobs = new();
    private readonly ILogger<ShellManager> _logger;

    // OSC 633 regex: matches 633;C (command start) and 633;D;{exit} (command finish)
    private static readonly Regex Osc633Regex = new(
        @"\x1b\]633;([CD]);?(\d*)\x07",
        RegexOptions.Compiled);

    // Current output buffer state
    private readonly StringBuilder _currentOutput = new();
    // Buffer for raw input to parse split escape sequences
    private readonly StringBuilder _parseBuffer = new();
    private TaskCompletionSource<int>? _currentCommandTcs;
    private bool _capturing;

    public ShellManager(ILogger<ShellManager> logger)
    {
        _logger = logger;

        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = "-i", // Interactive mode needed for PROMPT_COMMAND
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.GetEnvironmentVariable("HOME") ?? "/"
        };

        // Environment variables to support OSC 633 if needed, though we inject via PROMPT_COMMAND

        _process = new Process { StartInfo = startInfo };
        _process.Start();

        _logger.LogInformation("Started persistent shell process (PID: {Pid})", _process.Id);

        // Initialize shell integration
        // We do this immediately. 
        // Note: Writing to stdin might race with reading if we don't start readers first.

        StartReaders();

        // Inject OSC 633 integration
        // 633;A is prompt start (not used here)
        // 633;C is command start (trap DEBUG)
        // 633;D is command finished (PROMPT_COMMAND)
        var integration = "PROMPT_COMMAND='printf \"\\x1b]633;D;$?\\x07\"'; trap 'printf \"\\x1b]633;C\\x07\"' DEBUG\n";

        // We fire and forget this initialization write. 
        // Ideally we would wait for a "ready" signal, but for simplicity we assume it applies before first command.
        _process.StandardInput.WriteAsync(integration);
    }


    private void StartReaders()
    {
        Task.Run(async () => await ReadStreamAsync(_process.StandardOutput));
        Task.Run(async () => await ReadStreamAsync(_process.StandardError));
    }

    private async Task ReadStreamAsync(StreamReader reader)
    {
        var buffer = new char[1024];
        try
        {
            while (!_process.HasExited)
            {
                var read = await reader.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0) break;

                var text = new string(buffer, 0, read);
                ProcessOutput(text);
            }
        }
        catch { /* Ignore */ }
    }

    private void ProcessOutput(string text)
    {
        lock (_parseBuffer)
        {
            _parseBuffer.Append(text);
            var raw = _parseBuffer.ToString();

            var lastProcessedIndex = 0;

            foreach (Match match in Osc633Regex.Matches(raw))
            {
                var code = match.Groups[1].Value;
                var data = match.Groups[2].Value;

                // Add text before this sequence only if we're capturing (between C and D)
                if (_capturing && match.Index > lastProcessedIndex)
                {
                    lock (_currentOutput)
                    {
                        _currentOutput.Append(raw, lastProcessedIndex, match.Index - lastProcessedIndex);
                    }
                }

                switch (code)
                {
                    case "C":
                        // Command started - begin capturing output
                        _capturing = true;
                        // If we just started capturing, we might want to clear previous buffer?
                        // But usually we clear at Execute start.
                        break;

                    case "D":
                        // Command finished
                        if (int.TryParse(data, out var exitCode))
                        {
                            _capturing = false;
                            _currentCommandTcs?.TrySetResult(exitCode);
                        }
                        break;
                }

                lastProcessedIndex = match.Index + match.Length;
            }

            // Check for incomplete sequence at end (escape without bell)
            var escIndex = raw.LastIndexOf('\x1b');
            if (escIndex >= lastProcessedIndex && !raw.Substring(escIndex).Contains('\x07'))
            {
                // Incomplete sequence - keep in buffer for next read
                // Only capture remaining text if we're between C and D
                if (_capturing)
                {
                    lock (_currentOutput)
                    {
                        _currentOutput.Append(raw, lastProcessedIndex, escIndex - lastProcessedIndex);
                    }
                }
                _parseBuffer.Clear();
                _parseBuffer.Append(raw.Substring(escIndex));
            }
            else
            {
                // All sequences complete - only capture if between C and D
                if (_capturing && lastProcessedIndex < raw.Length)
                {
                    lock (_currentOutput)
                    {
                        _currentOutput.Append(raw, lastProcessedIndex, raw.Length - lastProcessedIndex);
                    }
                }
                _parseBuffer.Clear();
            }
        }
    }

    public async Task<(int ExitCode, string Output)> ExecuteForegroundAsync(string command, TimeSpan timeout)
    {
        await _lock.WaitAsync();
        try
        {
            _logger.LogInformation("Executing foreground command (Timeout: {Timeout}ms): {Command}", timeout.TotalMilliseconds, command);

            lock (_currentOutput) { _currentOutput.Clear(); }

            _capturing = false; // Reset state
            _currentCommandTcs = new TaskCompletionSource<int>();

            // Just send the command (plus newline)
            await _process.StandardInput.WriteAsync(command + "\n");
            await _process.StandardInput.FlushAsync();

            var completedTask = await Task.WhenAny(_currentCommandTcs.Task, Task.Delay(timeout));

            if (completedTask == _currentCommandTcs.Task)
            {
                var exitCode = await _currentCommandTcs.Task;
                string output;
                lock (_currentOutput)
                {
                    output = _currentOutput.ToString().TrimEnd();
                }

                _logger.LogInformation("Command completed. ExitCode: {ExitCode}, OutputLength: {Length}", exitCode, output.Length);
                return (exitCode, output);
            }
            else
            {
                _logger.LogWarning("Command timed out after {Timeout}ms: {Command}", timeout.TotalMilliseconds, command);
                throw new TimeoutException("Command timed out");
            }
        }
        catch (Exception ex) when (ex is not TimeoutException)
        {
            _logger.LogError(ex, "Error executing foreground command: {Command}", command);
            throw;
        }
        finally
        {
            _currentCommandTcs = null;
            _lock.Release();
        }
    }

    public async Task<string> ExecuteBackgroundAsync(string command)
    {
        _logger.LogInformation("Executing background command: {Command}", command);

        // For background, we just run a nohup command foreground-style to launch it, 
        // then capture the PID it echoes.
        // Even with OSC integration, this works fine because the foreground command 
        // is the "nohup ... & echo $!" line.

        var guid = Guid.NewGuid().ToString();
        // Ensure background output directory exists
        var outputDir = "/tmp/adc_background";
        Directory.CreateDirectory(outputDir);
        var outputFile = Path.Combine(outputDir, $"{guid}.log");

        var escaped = command.Replace("'", "'\\''");
        var bgCmd = $"nohup sh -c '{escaped}' > {outputFile} 2>&1 & echo $!";

        var (exitCode, output) = await ExecuteForegroundAsync(bgCmd, TimeSpan.FromSeconds(10));

        if (exitCode != 0)
        {
            _logger.LogError("Failed to start background process. ExitCode: {ExitCode}, Output: {Output}", exitCode, output);
            throw new Exception($"Failed to start background process: {output}");
        }

        var pidStr = output.Trim();
        if (!int.TryParse(pidStr, out var pid))
        {
            var match = Regex.Match(output, @"(\d+)\s*$");
            if (match.Success) pid = int.Parse(match.Groups[1].Value);
        }

        _logger.LogInformation("Started background job {JobId} (PID: {Pid}) logging to {LogPath}", guid, pid, outputFile);

        var job = new BackgroundJob(guid, pid, outputFile, command);
        _backgroundJobs[guid] = job;

        return guid;
    }

    public BackgroundJob? GetJob(string jobId)
    {
        _backgroundJobs.TryGetValue(jobId, out var job);
        return job;
    }

    public void KillJob(string jobId)
    {
        if (_backgroundJobs.TryGetValue(jobId, out var job))
        {
            Task.Run(() => ExecuteForegroundAsync($"kill -9 {job.Pid}", TimeSpan.FromSeconds(5)));
        }
    }

    public void Dispose()
    {
        _process?.Kill();
        _process?.Dispose();
    }
}

public record BackgroundJob(string Id, int Pid, string LogPath, string Command);
