// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Plugins.Models.WorkspaceTools;

/// <summary>
/// Represents an active terminal session with OSC 633 shell integration.
/// Provides deterministic command boundary detection and exit code capture.
/// </summary>
public class TerminalSession : IDisposable
{
    // OSC 633 regex: matches 633;C (command start) and 633;D;{exit} (command finish)
    private static readonly Regex Osc633Regex = new(
        @"\x1b\]633;([CD]);?(\d*)\x07",
        RegexOptions.Compiled);

    /// <summary>
    /// Simple session ID format: "term-1", "term-2", etc.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// The underlying shell process.
    /// </summary>
    public Process? Process { get; set; }

    /// <summary>
    /// Memory-efficient output buffer (first 500 + last 1000 chars).
    /// </summary>
    public ChunkedOutputBuffer OutputBuffer { get; } = new();

    /// <summary>
    /// The last command executed in this session.
    /// </summary>
    public string LastCommand { get; private set; } = string.Empty;

    /// <summary>
    /// Exit code from the last completed command.
    /// </summary>
    public int? LastExitCode { get; private set; }

    /// <summary>
    /// When the session was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the process is still running.
    /// </summary>
    public bool IsAlive => Process != null && !Process.HasExited;

    /// <summary>
    /// Event raised when the process exits unexpectedly.
    /// </summary>
    public event EventHandler? ProcessExited;

    // Concurrency control - only one command at a time
    private readonly SemaphoreSlim _commandSemaphore = new(1, 1);

    // Background reader task
    private Task? _readerTask;
    private CancellationTokenSource _readerCts = new();

    // Command completion signaling (set when 633;D received)
    private TaskCompletionSource<int>? _commandCompletion;
    private readonly object _completionLock = new();

    // OSC 633 parsing state (handles incomplete sequences at buffer boundaries)
    private readonly StringBuilder _parseBuffer = new();
    private readonly object _parseLock = new();

    // Output capture state - only capture between 633;C and 633;D
    private bool _capturing;

    private bool _disposed;

    /// <summary>
    /// Starts the background reader tasks for stdout and stderr.
    /// </summary>
    public void StartReader()
    {
        if (Process == null)
        {
            throw new InvalidOperationException("Process must be set before starting reader");
        }

        _readerTask = Task.Run(async () =>
        {
            try
            {
                // Read stdout and stderr in parallel, merging into same buffer
                var stdoutTask = ReadStreamAsync(Process.StandardOutput, _readerCts.Token);
                var stderrTask = ReadStreamAsync(Process.StandardError, _readerCts.Token);
                await Task.WhenAll(stdoutTask, stderrTask);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            finally
            {
                ProcessExited?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private async Task ReadStreamAsync(StreamReader reader, CancellationToken ct)
    {
        var buffer = new char[1024];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var count = await reader.ReadAsync(buffer, 0, buffer.Length);
                if (count == 0)
                {
                    break; // EOF - process exited
                }
                ProcessOutput(new string(buffer, 0, count));
            }
        }
        catch (IOException)
        {
            // Process terminated
        }
        catch (ObjectDisposedException)
        {
            // Reader disposed
        }
    }

    /// <summary>
    /// Processes raw output, strips OSC 633 sequences, and signals command completion.
    /// </summary>
    private void ProcessOutput(string text)
    {
        lock (_parseLock)
        {
            _parseBuffer.Append(text);
            var raw = _parseBuffer.ToString();

            var lastProcessedIndex = 0;
            var cleanOutput = new StringBuilder();

            foreach (Match match in Osc633Regex.Matches(raw))
            {
                var code = match.Groups[1].Value;
                var data = match.Groups[2].Value;

                // Add text before this sequence only if we're capturing (between C and D)
                if (_capturing && match.Index > lastProcessedIndex)
                {
                    cleanOutput.Append(raw, lastProcessedIndex, match.Index - lastProcessedIndex);
                }

                switch (code)
                {
                    case "C":
                        // Command started - begin capturing output
                        _capturing = true;
                        break;

                    case "D":
                        // Command finished - keep capturing to include trailing prompt
                        // (prompt shows agent the current directory)
                        if (int.TryParse(data, out var exitCode))
                        {
                            LastExitCode = exitCode;
                            lock (_completionLock)
                            {
                                _commandCompletion?.TrySetResult(exitCode);
                            }
                        }
                        break;
                }

                lastProcessedIndex = match.Index + match.Length;
            }

            // Check for incomplete sequence at end (escape without bell)
            var escIndex = raw.LastIndexOf('\x1b');
            if (escIndex >= lastProcessedIndex && !raw[escIndex..].Contains('\x07'))
            {
                // Incomplete sequence - keep in buffer for next read
                // Only capture remaining text if we're between C and D
                if (_capturing)
                {
                    cleanOutput.Append(raw, lastProcessedIndex, escIndex - lastProcessedIndex);
                }
                _parseBuffer.Clear();
                _parseBuffer.Append(raw[escIndex..]);
            }
            else
            {
                // All sequences complete - only capture if between C and D
                if (_capturing && lastProcessedIndex < raw.Length)
                {
                    cleanOutput.Append(raw, lastProcessedIndex, raw.Length - lastProcessedIndex);
                }
                _parseBuffer.Clear();
            }

            // Append clean output to chunked buffer
            var clean = cleanOutput.ToString();
            if (!string.IsNullOrEmpty(clean))
            {
                OutputBuffer.Append(clean);
            }
        }
    }

    /// <summary>
    /// Executes a foreground command and waits for completion.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="timeout">Maximum time to wait for completion.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of (output, exitCode).</returns>
    public async Task<(string output, int exitCode)> ExecuteCommandAsync(
        string command,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        await _commandSemaphore.WaitAsync(ct);
        try
        {
            PrepareForCommand(command);
            await WriteLineAsync(command);

            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                // Grab the task reference under lock, but await OUTSIDE the lock
                // to avoid deadlock with ProcessOutput() which also needs this lock
                Task<int> completionTask;
                lock (_completionLock)
                {
                    completionTask = _commandCompletion!.Task;
                }

                var exitCode = await completionTask.WaitAsync(linkedCts.Token);
                return (OutputBuffer.GetOutput(), exitCode);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException($"Command timed out after {timeout}");
            }
        }
        finally
        {
            lock (_completionLock)
            {
                _commandCompletion = null;
            }

            // Reset capturing state in case we timed out mid-command
            lock (_parseLock)
            {
                _capturing = false;
            }

            _commandSemaphore.Release();
        }
    }

    /// <summary>
    /// Executes a background command with output redirected to a file.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <returns>String describing PID and output file location.</returns>
    public async Task<string> ExecuteBackgroundCommandAsync(string command)
    {
        await _commandSemaphore.WaitAsync();
        try
        {
            // Ensure background output directory exists
            var outputDir = TerminalConfiguration.BackgroundOutputDir;
            var outputFile = $"{outputDir}/bg_{Process?.Id ?? 0}_{DateTime.UtcNow.Ticks}.log";

            // Wrap command: nohup + redirect + background + echo PID
            var escapedCommand = command.Replace("'", "'\\''");
            var wrappedCommand = $"mkdir -p {outputDir} && nohup sh -c '{escapedCommand}' > {outputFile} 2>&1 & echo $!";

            PrepareForCommand(command);
            await WriteLineAsync(wrappedCommand);

            // Wait briefly for PID echo
            await Task.Delay(TerminalConfiguration.BackgroundPidWait);

            var pid = OutputBuffer.GetOutput().Trim();
            return $"Background process started (PID: {pid}). Output file: {outputFile}";
        }
        finally
        {
            _commandSemaphore.Release();
        }
    }

    /// <summary>
    /// Verifies that shell integration is working by running a probe command.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for probe response.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task VerifyIntegrationAsync(TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            // Use the same code path as normal command execution
            var (output, exitCode) = await ExecuteCommandAsync("echo __probe_complete__", timeout, ct);

            // Verify we got the expected output
            if (!output.Contains("__probe_complete__"))
            {
                throw new TerminalInitializationException(
                    $"Shell integration probe returned unexpected output: {output}");
            }
        }
        catch (TimeoutException)
        {
            throw new TerminalInitializationException(
                "Shell integration failed to respond within timeout. OSC 633 sequences not detected.");
        }
    }

    /// <summary>
    /// Cancels the currently running command by sending SIGINT (Ctrl+C).
    /// </summary>
    public void CancelCurrentCommand()
    {
        if (!IsAlive)
        {
            return;
        }

        try
        {
            // Send Ctrl+C character
            Process!.StandardInput.Write('\x03');
            Process.StandardInput.Flush();

            lock (_completionLock)
            {
                _commandCompletion?.TrySetCanceled();
            }
        }
        catch (Exception)
        {
            // Process may have exited
        }
    }

    /// <summary>
    /// Prepares for a new command execution.
    /// </summary>
    private void PrepareForCommand(string command)
    {
        OutputBuffer.Clear();
        LastCommand = command;
        LastExitCode = null;

        // Reset capturing state before new command
        // This ensures we don't capture the echoed command (before 633;C)
        lock (_parseLock)
        {
            _capturing = false;
        }

        lock (_completionLock)
        {
            _commandCompletion = new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>
    /// Writes a line to the process stdin.
    /// </summary>
    public async Task WriteLineAsync(string text)
    {
        if (Process?.StandardInput == null)
        {
            throw new InvalidOperationException("Process stdin not available");
        }

        // Use \n explicitly - bash expects Unix line endings, not Windows \r\n
        await Process.StandardInput.WriteAsync(text + "\n");
        await Process.StandardInput.FlushAsync();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            try
            {
                _readerCts.Cancel();
                _readerCts.Dispose();

                if (Process != null && !Process.HasExited)
                {
                    Process.Kill(entireProcessTree: true);
                }

                Process?.Dispose();
                _commandSemaphore.Dispose();
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        _disposed = true;
    }
}
