# Terminal Session Management Implementation

This document describes the implementation of terminal session management for the VS Code-like agent tools in the SRE Agent Runtime, using OSC 633 shell integration for deterministic command detection.

## Design Overview

- **One session per thread** (keyed by `Guid` from `IThreadContextAccessor`)
- **OSC 633 shell integration** for deterministic command boundaries and exit codes
- **Chunked output buffer** for memory-efficient capture of long outputs (first 500 + last 1000 chars)
- **Git Bash support** on Windows (tested and verified)
- **Background commands** redirect output to files in `/sandbox/tmp/`
- **stderr merged into stdout** for unified output
- **Session auto-restart** on unexpected process death
- **SIGINT support** via `CancelCurrentCommand()` for user cancellation

## Design Decisions (Interview Summary)

| Topic | Decision |
|-------|----------|
| Session initialization | Wait for probe command's `633;D` before returning (5s timeout, retry once, then throw) |
| Concurrent calls | SemaphoreSlim(1,1) protects ExecuteCommandAsync |
| Background commands | Redirect to files (`/sandbox/tmp/bg_{pid}.log`), return PID in string message |
| Process death | Auto-restart session unless disposing |
| Working directory | Session maintains own cwd; no `cwd` param exposed to tools |
| OSC 633 spoofing | Accept as edge case (not worth defending) |
| stderr handling | Merge into stdout via `RedirectStandardError = true` + interleaved read |
| Cancel command | `CancelCurrentCommand()` sends SIGINT; hookable from user cancellation token |
| `get_terminal_output` | **Removed** - background uses files, foreground returns directly |
| `terminal_last_command` | **Kept** - agent can ignore if not needed |
| Background return format | String message (e.g., "Started PID 1234, output: /sandbox/tmp/bg_1234.log") |
| Init timeout | 5 seconds; retry once on failure, then throw `TerminalInitializationException` |

## Git Bash Compatibility

**Tested on:** Git Bash (Git for Windows) with `System.Diagnostics.Process` redirected I/O.

| Feature | Result |
|---------|--------|
| `trap DEBUG` emits `633;C` | ✅ Works |
| `PROMPT_COMMAND` emits `633;D;$?` | ✅ Works |
| Exit code capture (0, 1, 2, etc.) | ✅ Works |
| Interactive mode (`-i` flag) | ✅ Required |

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    VsCodeToolsPlugin                        │
│  - run_in_terminal → ExecuteCommandAsync                    │
│  - terminal_last_command → GetLastCommand                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  TerminalSessionManager                     │
│  - ConcurrentDictionary<Guid, TerminalSession>              │
│  - GetOrCreateSessionAsync()                                │
│  - ExecuteCommandAsync(command, isBackground)               │
│  - CancelCurrentCommand()                                   │
│  - Uses IThreadContextAccessor.CurrentThreadId (Guid)       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     TerminalSession                         │
│  - Process (bash -i with redirected I/O)                   │
│  - ChunkedOutputBuffer (first 500 + last 1000 chars)       │
│  - CommandCompletionSource (signals on 633;D)              │
│  - CommandSemaphore (SemaphoreSlim 1,1)                    │
│  - LastExitCode, LastCommand                               │
│  - BackgroundReaderTask                                    │
└─────────────────────────────────────────────────────────────┘
```

## Session Per Thread

```csharp
public class TerminalSessionManager : IDisposable
{
    private readonly ConcurrentDictionary<Guid, TerminalSession> _sessions = new();
    private readonly IThreadContextAccessor _threadContextAccessor;
    private readonly ILogger<TerminalSessionManager> _logger;
    private int _sessionCounter;
    private bool _disposing;

    public TerminalSessionManager(
        IThreadContextAccessor threadContextAccessor,
        ILogger<TerminalSessionManager> logger)
    {
        _threadContextAccessor = threadContextAccessor;
        _logger = logger;
    }

    public async Task<TerminalSession> GetOrCreateSessionAsync(CancellationToken ct = default)
    {
        var threadId = _threadContextAccessor.CurrentThreadId
            ?? throw new InvalidOperationException("No thread context");

        if (_sessions.TryGetValue(threadId, out var existing) && existing.IsAlive)
        {
            return existing;
        }

        // Session died or doesn't exist - create new
        var session = await CreateSessionWithRetryAsync(ct);
        _sessions[threadId] = session;

        // Subscribe to process death for auto-restart
        session.ProcessExited += (s, e) => OnProcessExited(threadId, session);

        return session;
    }

    private async Task<TerminalSession> CreateSessionWithRetryAsync(CancellationToken ct)
    {
        const int maxAttempts = 2;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await CreateSessionAsync(ct);
            }
            catch (TerminalInitializationException) when (attempt < maxAttempts)
            {
                _logger.LogWarning("Session init failed, retrying ({Attempt}/{Max})", attempt, maxAttempts);
            }
        }
        throw new TerminalInitializationException("Failed to initialize terminal after retries");
    }

    private async Task<TerminalSession> CreateSessionAsync(CancellationToken ct)
    {
        var id = $"term-{Interlocked.Increment(ref _sessionCounter)}";
        var shell = GetShellPath();

        var psi = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = "-i",  // Interactive mode required for PROMPT_COMMAND
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,  // Merge stderr into stdout
            CreateNoWindow = true,
            WorkingDirectory = "/sandbox"  // Default working directory
        };

        var process = Process.Start(psi)
            ?? throw new TerminalInitializationException("Failed to start bash process");

        var session = new TerminalSession
        {
            Id = id,
            Process = process,
            CreatedAt = DateTime.UtcNow
        };

        // Start background reader (merges stdout + stderr)
        session.StartReader();

        // Inject shell integration
        var integration = """
            PROMPT_COMMAND='printf "\x1b]633;D;$?\x07"'
            trap 'printf "\x1b]633;C\x07"' DEBUG
            """;
        await session.WriteLineAsync(integration);

        // Wait for integration to be ready (probe command)
        await session.VerifyIntegrationAsync(TimeSpan.FromSeconds(5), ct);

        return session;
    }

    private void OnProcessExited(Guid threadId, TerminalSession session)
    {
        if (_disposing) return;

        _logger.LogWarning("Terminal {Id} process exited unexpectedly, will recreate on next use", session.Id);
        _sessions.TryRemove(threadId, out _);
    }

    public TerminalSession? GetCurrentSession()
    {
        var threadId = _threadContextAccessor.CurrentThreadId;
        if (threadId == null) return null;

        _sessions.TryGetValue(threadId.Value, out var session);
        return session?.IsAlive == true ? session : null;
    }

    private static string GetShellPath()
    {
        if (OperatingSystem.IsWindows())
        {
            // Git Bash
            var gitBash = @"C:\Program Files\Git\bin\bash.exe";
            if (File.Exists(gitBash)) return gitBash;
            throw new InvalidOperationException("Git Bash not found");
        }
        return "/bin/bash";
    }
}

public class TerminalInitializationException : Exception
{
    public TerminalInitializationException(string message) : base(message) { }
    public TerminalInitializationException(string message, Exception inner) : base(message, inner) { }
}
```

## OSC 633 Shell Integration

### Protocol

| Sequence | Meaning | Emitted By |
|----------|---------|------------|
| `633;C` | Command started | `trap DEBUG` |
| `633;D;{n}` | Command finished with exit code | `PROMPT_COMMAND` |

### Integration Script (2 lines)

```bash
PROMPT_COMMAND='printf "\x1b]633;D;$?\x07"'
trap 'printf "\x1b]633;C\x07"' DEBUG
```

## Chunked Output Buffer

Memory-efficient capture of potentially large outputs.

### Strategy

```
┌─────────────────────────────────────────────────────────────────┐
│  HEAD (first N chars)           │  TAIL RING (last 2N chars)    │
│  ┌─────────────────────────┐    │  ┌───┐ ┌───┐ ┌───┐ ┌───┐     │
│  │ First 500 chars         │    │  │ 4 │→│ 5 │→│ 6 │→│ 7 │     │
│  │ (StringBuilder)         │    │  └───┘ └───┘ └───┘ └───┘     │
│  └─────────────────────────┘    │  (Queue<string>, 2KB chunks)  │
│  Stop appending when full       │  Drops oldest when > 4 chunks │
└─────────────────────────────────────────────────────────────────┘
```

### Implementation

```csharp
public class ChunkedOutputBuffer
{
    private const int HeadSize = 500;           // First N chars to keep
    private const int TailSize = 1000;          // Last 2N chars to keep
    private const int ChunkSize = 2048;         // 2KB chunks
    private const int MaxTailChunks = 4;        // Keep 4 chunks = 8KB max

    private readonly StringBuilder _head = new(HeadSize);
    private readonly Queue<string> _tailChunks = new();
    private StringBuilder _currentChunk = new(ChunkSize);
    private bool _headFull;
    private long _totalBytesReceived;

    public void Append(string text)
    {
        _totalBytesReceived += Encoding.UTF8.GetByteCount(text);

        foreach (var ch in text)
        {
            // Fill head first
            if (!_headFull)
            {
                _head.Append(ch);
                if (_head.Length >= HeadSize)
                {
                    _headFull = true;
                }
            }

            // Always append to current chunk (for tail)
            _currentChunk.Append(ch);

            // Rotate chunks when full
            if (_currentChunk.Length >= ChunkSize)
            {
                _tailChunks.Enqueue(_currentChunk.ToString());
                _currentChunk.Clear();

                // Drop oldest if too many
                while (_tailChunks.Count > MaxTailChunks)
                {
                    _tailChunks.Dequeue();
                }
            }
        }
    }

    public string GetOutput()
    {
        // Build tail from chunks + current
        var tailBuilder = new StringBuilder();
        foreach (var chunk in _tailChunks)
        {
            tailBuilder.Append(chunk);
        }
        tailBuilder.Append(_currentChunk);

        var tail = tailBuilder.ToString();

        // Take only last TailSize chars
        if (tail.Length > TailSize)
        {
            tail = tail.Substring(tail.Length - TailSize);
        }

        var headStr = _head.ToString();

        // Small output - no truncation needed
        if (!_headFull && _tailChunks.Count == 0)
        {
            return headStr;
        }

        // Check if output fits in head + tail without overlap
        if (_totalBytesReceived <= HeadSize + TailSize)
        {
            // Avoid duplicating content
            var totalChars = headStr.Length + tail.Length;
            if (totalChars <= HeadSize + TailSize)
            {
                return headStr + tail.Substring(Math.Min(headStr.Length, tail.Length));
            }
        }

        // Large output - show truncation
        return $"{headStr}\n\n... [{_totalBytesReceived:N0} bytes total] ...\n\n{tail}";
    }

    public void Clear()
    {
        _head.Clear();
        _tailChunks.Clear();
        _currentChunk.Clear();
        _headFull = false;
        _totalBytesReceived = 0;
    }

    public long TotalBytesReceived => _totalBytesReceived;
}
```

### Memory Analysis

| Component | Size |
|-----------|------|
| Head buffer | 500 chars × 2 bytes = 1 KB |
| Tail chunks | 4 × 2KB = 8 KB |
| Current chunk | 2 KB |
| **Total per session** | **~11 KB** |

## Session Model

```csharp
public class TerminalSession : IDisposable
{
    public string Id { get; init; }                          // "term-1", "term-2"
    public Process Process { get; init; }                    // bash -i process
    public ChunkedOutputBuffer OutputBuffer { get; } = new(); // Efficient capture
    public string LastCommand { get; set; } = "";            // For terminal_last_command
    public int? LastExitCode { get; set; }                   // From 633;D
    public DateTime CreatedAt { get; init; }

    // Concurrency control
    private readonly SemaphoreSlim _commandSemaphore = new(1, 1);

    // Background reader
    private Task? _readerTask;
    private CancellationTokenSource _readerCts = new();

    // Command completion signaling
    private TaskCompletionSource<int>? _commandCompletion;
    private readonly object _lock = new();

    // Parsing state
    private readonly StringBuilder _parseBuffer = new();

    // Process health
    public bool IsAlive => !Process.HasExited;
    public event EventHandler? ProcessExited;

    public void StartReader()
    {
        _readerTask = Task.Run(async () =>
        {
            try
            {
                // Merge stdout and stderr
                var stdoutTask = ReadStreamAsync(Process.StandardOutput);
                var stderrTask = ReadStreamAsync(Process.StandardError);
                await Task.WhenAll(stdoutTask, stderrTask);
            }
            catch (OperationCanceledException) { }
            finally
            {
                ProcessExited?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private async Task ReadStreamAsync(StreamReader reader)
    {
        var buffer = new char[1024];
        while (!_readerCts.Token.IsCancellationRequested)
        {
            var count = await reader.ReadAsync(buffer, 0, buffer.Length);
            if (count == 0) break;  // EOF
            ProcessOutput(new string(buffer, 0, count));
        }
    }

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
                var exitCode = await _commandCompletion!.Task.WaitAsync(linkedCts.Token);
                return (OutputBuffer.GetOutput(), exitCode);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException($"Command timed out after {timeout}");
            }
        }
        finally
        {
            _commandSemaphore.Release();
        }
    }

    public async Task<string> ExecuteBackgroundCommandAsync(string command)
    {
        await _commandSemaphore.WaitAsync();
        try
        {
            // Redirect to file
            var outputFile = $"/sandbox/tmp/bg_{Process.Id}_{DateTime.UtcNow.Ticks}.log";
            var wrappedCommand = $"nohup sh -c '{command.Replace("'", "'\\''")}' > {outputFile} 2>&1 & echo $!";

            PrepareForCommand(command);
            await WriteLineAsync(wrappedCommand);

            // Wait briefly for PID echo
            await Task.Delay(500);

            var pid = OutputBuffer.GetOutput().Trim();
            return $"Background process started (PID: {pid}). Output file: {outputFile}";
        }
        finally
        {
            _commandSemaphore.Release();
        }
    }

    public async Task VerifyIntegrationAsync(TimeSpan timeout, CancellationToken ct)
    {
        PrepareForCommand("integration_probe");
        await WriteLineAsync("echo probe_complete");

        using var cts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);

        try
        {
            await _commandCompletion!.Task.WaitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TerminalInitializationException(
                "Shell integration failed to respond within timeout");
        }

        OutputBuffer.Clear();  // Clear probe output
    }

    public void CancelCurrentCommand()
    {
        if (!IsAlive) return;

        // Send SIGINT (Ctrl+C)
        if (OperatingSystem.IsWindows())
        {
            // On Windows, write Ctrl+C character
            Process.StandardInput.Write('\x03');
            Process.StandardInput.Flush();
        }
        else
        {
            // On Unix, send SIGINT
            Process.Kill(Signum.SIGINT);
        }

        _commandCompletion?.TrySetCanceled();
    }

    private void PrepareForCommand(string command)
    {
        OutputBuffer.Clear();
        LastCommand = command;
        LastExitCode = null;
        _commandCompletion = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public async Task WriteLineAsync(string text)
    {
        await Process.StandardInput.WriteLineAsync(text);
        await Process.StandardInput.FlushAsync();
    }
}
```

## OSC 633 Parsing

```csharp
private static readonly Regex Osc633Regex = new(
    @"\x1b\]633;([CD]);?(\d*)\x07",
    RegexOptions.Compiled);

private void ProcessOutput(string text)
{
    lock (_lock)
    {
        _parseBuffer.Append(text);
        var raw = _parseBuffer.ToString();

        var lastProcessedIndex = 0;
        var cleanOutput = new StringBuilder();

        foreach (Match match in Osc633Regex.Matches(raw))
        {
            // Add text before this sequence
            if (match.Index > lastProcessedIndex)
            {
                cleanOutput.Append(raw, lastProcessedIndex, match.Index - lastProcessedIndex);
            }

            var code = match.Groups[1].Value;
            var data = match.Groups[2].Value;

            switch (code)
            {
                case "C":  // Command started
                    break;

                case "D":  // Command finished
                    if (int.TryParse(data, out var exitCode))
                    {
                        LastExitCode = exitCode;
                        _commandCompletion?.TrySetResult(exitCode);
                    }
                    break;
            }

            lastProcessedIndex = match.Index + match.Length;
        }

        // Check for incomplete sequence at end
        var escIndex = raw.LastIndexOf('\x1b');
        if (escIndex >= lastProcessedIndex && !raw[escIndex..].Contains('\x07'))
        {
            cleanOutput.Append(raw, lastProcessedIndex, escIndex - lastProcessedIndex);
            _parseBuffer.Clear();
            _parseBuffer.Append(raw[escIndex..]);
        }
        else
        {
            if (lastProcessedIndex < raw.Length)
            {
                cleanOutput.Append(raw, lastProcessedIndex, raw.Length - lastProcessedIndex);
            }
            _parseBuffer.Clear();
        }

        // Append to chunked buffer
        var clean = cleanOutput.ToString();
        if (!string.IsNullOrEmpty(clean))
        {
            OutputBuffer.Append(clean);
        }
    }
}
```

## Command Execution Flow

### Foreground Commands

```
1. GetOrCreateSessionAsync()
2. Acquire SemaphoreSlim
3. PrepareForCommand() - new TaskCompletionSource, clear buffer
4. OutputBuffer.Clear()
5. Write command + "\n" to stdin
6. Reader sees 633;C (started)
7. Output streams, OSC stripped, appended to ChunkedOutputBuffer
8. Reader sees 633;D;{exit} (finished)
9. Signal TaskCompletionSource with exit code
10. Release SemaphoreSlim
11. Return (OutputBuffer.GetOutput(), exitCode)
```

### Background Commands

```
1. GetOrCreateSessionAsync()
2. Acquire SemaphoreSlim
3. Wrap command: nohup sh -c 'cmd' > /sandbox/tmp/bg_{pid}_{ticks}.log 2>&1 & echo $!
4. Write wrapped command to stdin
5. Wait 500ms for PID echo
6. Release SemaphoreSlim
7. Return string: "Background process started (PID: {pid}). Output file: {path}"
   (Agent uses read_file tool to check output)
```

### User Cancellation

```
1. User cancellation token fires
2. Call CancelCurrentCommand()
3. Send Ctrl+C (\x03) or SIGINT to process
4. TaskCompletionSource.TrySetCanceled()
5. SemaphoreSlim released, ready for next command
```

## Configuration

```csharp
public static class TerminalConfiguration
{
    public static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan InitTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan BackgroundPidWait = TimeSpan.FromMilliseconds(500);
    public const int MaxInitRetries = 2;  // Initial + 1 retry
    public const int HeadChars = 500;
    public const int TailChars = 1000;
    public const string BackgroundOutputDir = "/sandbox/tmp";
}
```

## Platform Support

| Platform | Shell | Notes |
|----------|-------|-------|
| Linux | `/bin/bash -i` | Full support |
| macOS | `/bin/bash -i` | Full support |
| Windows | Git Bash | Tested, works |

## Tools Provided

| Tool | Description |
|------|-------------|
| `run_in_terminal` | Execute command, return output + exit code (foreground) or PID + output file path (background) |
| `terminal_last_command` | Get the last command that was run |

**Removed Tools:**
- `get_terminal_output` - Not needed; foreground returns output directly, background writes to files
- `terminal_selection` - Not applicable in headless environment

## Files to Modify

| File | Changes |
|------|---------|
| `TerminalSession.cs` | SemaphoreSlim, merged stderr, CancelCurrentCommand, VerifyIntegrationAsync |
| `TerminalSessionManager.cs` | Remove cwd param, auto-restart, CreateSessionWithRetryAsync |
| `ChunkedOutputBuffer.cs` | New file (already designed) |
| `VsCodeToolsPlugin.cs` | Remove terminal_selection, remove get_terminal_output, remove cwd param from run_in_terminal |
| `ITerminalSession.cs` | Update interface for new methods |

## DI Registration

```csharp
services.AddSingleton<TerminalSessionManager>();
services.AddSingleton<IVsCodeToolsPlugin, VsCodeToolsPlugin>();
```

## Edge Cases

| Scenario | Handling |
|----------|----------|
| OSC 633 spoofing | Accept as edge case - agent command output could theoretically emit fake sequences |
| Process death mid-command | TaskCompletionSource never completes, timeout fires, session auto-recreated on next use |
| Concurrent tool calls | SemaphoreSlim serializes; second caller waits |
| Very large output | ChunkedOutputBuffer keeps first 500 + last 1000 chars (~11KB memory) |
| Init failure | Retry once with fresh process, then throw TerminalInitializationException |
