using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;

namespace Session.Proxy.Services;

/// <summary>
/// Service that manages WebSocket connections and proxies data between WebSocket clients and MCP server processes.
/// </summary>
public class McpProxyService
{
    private readonly ILogger<McpProxyService> _logger;
    private static readonly UTF8Encoding NoBomUtf8 = new(encoderShouldEmitUTF8Identifier: false);

    public McpProxyService(ILogger<McpProxyService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles a WebSocket connection by launching an MCP server process and proxying data bidirectionally.
    /// </summary>
    public async Task HandleWebSocketConnection(
        WebSocket webSocket,
        string command,
        string[] args,
        CancellationToken cancellationToken)
    {
        var connectionId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("Connection {ConnectionId}: Starting MCP proxy for command: {Command} {Args}",
            connectionId, command, string.Join(" ", args));

        Process? process = null;
        try
        {
            // Launch the MCP server process
            process = LaunchMcpProcess(command, args, connectionId);

            // Send "ok" message to indicate successful initialization
            await SendWebSocketMessage(webSocket, "ok", cancellationToken);
            _logger.LogInformation("Connection {ConnectionId}: MCP server launched successfully", connectionId);

            // Start bidirectional proxying
            await ProxyBidirectionally(webSocket, process, connectionId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection {ConnectionId}: Error launching or proxying MCP server", connectionId);

            // Try to send error message to client
            try
            {
                await SendWebSocketMessage(webSocket, $"Error: {ex.Message}", cancellationToken);
            }
            catch (Exception sendEx)
            {
                _logger.LogError(sendEx, "Connection {ConnectionId}: Failed to send error message to client", connectionId);
            }
        }
        finally
        {
            // Cleanup
            await CleanupConnection(webSocket, process, connectionId);
        }
    }

    /// <summary>
    /// Launches the MCP server process with stdio redirection.
    /// </summary>
    private Process LaunchMcpProcess(string command, string[] args, string connectionId)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Environment.CurrentDirectory,
                StandardInputEncoding = NoBomUtf8,
                StandardOutputEncoding = NoBomUtf8,
                StandardErrorEncoding = NoBomUtf8
            };

            // On Windows, wrap commands with cmd.exe to ensure proper PATH resolution
            // This matches the behavior of StdioClientTransport in the official SDK
            if (OperatingSystem.IsWindows())
            {
                startInfo.FileName = "cmd.exe";

                // Build the command line: cmd.exe /c "command arg1 arg2 ..."
                var commandLine = new StringBuilder();
                commandLine.Append(command);
                foreach (var arg in args)
                {
                    commandLine.Append(' ');
                    // Escape arguments that contain spaces or special characters
                    if (arg.Contains(' ') || arg.Contains('"'))
                    {
                        commandLine.Append('"');
                        commandLine.Append(arg.Replace("\"", "\\\""));
                        commandLine.Append('"');
                    }
                    else
                    {
                        commandLine.Append(arg);
                    }
                }

                startInfo.ArgumentList.Add("/c");
                startInfo.ArgumentList.Add(commandLine.ToString());

                _logger.LogInformation("Connection {ConnectionId}: Launching via cmd.exe: {Command}",
                    connectionId, commandLine.ToString());
            }
            else
            {
                // On Unix-like systems, use the command directly
                startInfo.FileName = command;
                foreach (var arg in args)
                {
                    startInfo.ArgumentList.Add(arg);
                }

                _logger.LogInformation("Connection {ConnectionId}: Launching directly: {Command} {Args}",
                    connectionId, command, string.Join(" ", args));
            }

            var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start process: {command}");
            }

            _logger.LogInformation("Connection {ConnectionId}: Process started with PID {ProcessId}",
                connectionId, process.Id);

            return process;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection {ConnectionId}: Failed to launch MCP process", connectionId);
            throw;
        }
    }

    /// <summary>
    /// Proxies data bidirectionally between WebSocket and MCP server process.
    /// </summary>
    private async Task ProxyBidirectionally(
        WebSocket webSocket,
        Process process,
        string connectionId,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Wrap streams with TextReader/TextWriter for line-based communication
        // Use UTF8 without BOM to match the official SDK behavior
        var stdinWriter = new StreamWriter(process.StandardInput.BaseStream, NoBomUtf8, leaveOpen: false)
        {
            AutoFlush = false, // We'll flush explicitly after each write
            NewLine = "\n" // MCP uses LF line endings
        };
        var stdoutReader = new StreamReader(process.StandardOutput.BaseStream, NoBomUtf8, leaveOpen: false);

        // Task to read from WebSocket and write to MCP server stdin
        var wsToMcpTask = Task.Run(async () =>
        {
            try
            {
                await ProxyWebSocketToMcp(webSocket, stdinWriter, connectionId, cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection {ConnectionId}: Error in WebSocket->MCP proxy", connectionId);
            }
            finally
            {
                cts.Cancel(); // Signal the other direction to stop
            }
        }, cts.Token);

        // Task to read from MCP server stdout and write to WebSocket
        var mcpToWsTask = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Connection {ConnectionId}: MCP process running: {IsRunning}, HasExited: {HasExited}",
                    connectionId, !process.HasExited, process.HasExited);
                await ProxyMcpToWebSocket(stdoutReader, webSocket, connectionId, cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection {ConnectionId}: Error in MCP->WebSocket proxy", connectionId);
            }
            finally
            {
                cts.Cancel(); // Signal the other direction to stop
            }
        }, cts.Token);

        // Task to read from MCP server stderr and log it
        var stderrTask = Task.Run(async () =>
        {
            try
            {
                await LogStderr(process, connectionId, cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection {ConnectionId}: Error reading stderr", connectionId);
            }
        }, cts.Token);

        // Wait for either proxy task to complete (or fail)
        await Task.WhenAny(wsToMcpTask, mcpToWsTask);

        // Cancel all tasks
        cts.Cancel();

        // Wait for all tasks to complete with a timeout
        try
        {
            await Task.WhenAll(wsToMcpTask, mcpToWsTask, stderrTask).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is triggered
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection {ConnectionId}: Error during bidirectional proxy shutdown", connectionId);
        }

        // Dispose text wrappers
        await stdinWriter.DisposeAsync();
        stdoutReader.Dispose();

        _logger.LogInformation("Connection {ConnectionId}: Bidirectional proxy completed", connectionId);
    }

    /// <summary>
    /// Reads messages from WebSocket and writes them to MCP server stdin as lines.
    /// </summary>
    private async Task ProxyWebSocketToMcp(
        WebSocket webSocket,
        StreamWriter stdinWriter,
        string connectionId,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        while (!cancellationToken.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                _logger.LogInformation("Connection {ConnectionId}: WebSocket closed prematurely", connectionId);
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogInformation("Connection {ConnectionId}: WebSocket close message received", connectionId);
                break;
            }

            if (result.MessageType == WebSocketMessageType.Text || result.MessageType == WebSocketMessageType.Binary)
            {
                // Read the complete message
                using var ms = new MemoryStream();
                ms.Write(buffer, 0, result.Count);

                while (!result.EndOfMessage)
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    ms.Write(buffer, 0, result.Count);
                }

                var messageText = Encoding.UTF8.GetString(ms.ToArray());
                _logger.LogDebug("Connection {ConnectionId}: Received from WebSocket: {Message}",
                    connectionId, messageText.Length > 200 ? messageText[..200] + "..." : messageText);

                // Write to MCP server stdin as a line (MCP protocol is line-based JSON-RPC)
                try
                {
                    await stdinWriter.WriteLineAsync(messageText);
                    await stdinWriter.FlushAsync(cancellationToken);
                    _logger.LogDebug("Connection {ConnectionId}: Wrote and flushed to MCP stdin", connectionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Connection {ConnectionId}: Error writing to MCP process stdin", connectionId);
                    break;
                }
            }
        }

        _logger.LogInformation("Connection {ConnectionId}: WebSocket->MCP proxy completed", connectionId);
    }

    /// <summary>
    /// Reads lines from MCP server stdout and writes them to WebSocket.
    /// </summary>
    private async Task ProxyMcpToWebSocket(
        StreamReader stdoutReader,
        WebSocket webSocket,
        string connectionId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Connection {ConnectionId}: Starting to read from MCP stdout", connectionId);

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                _logger.LogDebug("Connection {ConnectionId}: Waiting for line from MCP stdout...", connectionId);
                line = await stdoutReader.ReadLineAsync(cancellationToken);
                _logger.LogDebug("Connection {ConnectionId}: ReadLineAsync returned: {IsNull}", connectionId, line == null);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Connection {ConnectionId}: ReadLineAsync cancelled", connectionId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection {ConnectionId}: Error reading from MCP process stdout", connectionId);
                break;
            }

            if (line == null)
            {
                // End of stream
                _logger.LogInformation("Connection {ConnectionId}: MCP process stdout closed (null line)", connectionId);
                break;
            }

            // Skip empty lines (following SDK pattern)
            if (string.IsNullOrWhiteSpace(line))
            {
                _logger.LogDebug("Connection {ConnectionId}: Skipping empty line from MCP stdout", connectionId);
                continue;
            }

            _logger.LogInformation("Connection {ConnectionId}: Read from MCP stdout: {Message}",
                connectionId, line.Length > 200 ? line[..200] + "..." : line);

            // Send to WebSocket as a complete message
            try
            {
                var bytes = Encoding.UTF8.GetBytes(line);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);

                _logger.LogDebug("Connection {ConnectionId}: Sent to WebSocket: {Message}",
                    connectionId, line.Length > 200 ? line[..200] + "..." : line);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection {ConnectionId}: Error sending to WebSocket", connectionId);
                break;
            }
        }

        _logger.LogInformation("Connection {ConnectionId}: MCP->WebSocket proxy completed", connectionId);
    }

    /// <summary>
    /// Reads stderr from the MCP process and logs it.
    /// </summary>
    private async Task LogStderr(Process process, string connectionId, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var stderr = process.StandardError.BaseStream;

        while (!cancellationToken.IsCancellationRequested && !process.HasExited)
        {
            int bytesRead;
            try
            {
                bytesRead = await stderr.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection {ConnectionId}: Error reading stderr", connectionId);
                break;
            }

            if (bytesRead == 0)
            {
                break;
            }

            var stderrText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            _logger.LogWarning("Connection {ConnectionId}: MCP stderr: {Stderr}", connectionId, stderrText);
        }
    }

    /// <summary>
    /// Sends a text message to the WebSocket.
    /// </summary>
    private async Task SendWebSocketMessage(WebSocket webSocket, string message, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        await webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }

    /// <summary>
    /// Cleans up the WebSocket connection and MCP server process.
    /// </summary>
    private async Task CleanupConnection(WebSocket webSocket, Process? process, string connectionId)
    {
        _logger.LogInformation("Connection {ConnectionId}: Cleaning up connection", connectionId);

        // Close WebSocket if still open
        if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
        {
            try
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Connection closed",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection {ConnectionId}: Error closing WebSocket", connectionId);
            }
        }

        // Terminate process if still running
        if (process != null && !process.HasExited)
        {
            try
            {
                _logger.LogInformation("Connection {ConnectionId}: Terminating MCP process (PID {ProcessId})",
                    connectionId, process.Id);

                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection {ConnectionId}: Error terminating MCP process", connectionId);
            }
        }

        // Dispose process
        process?.Dispose();

        _logger.LogInformation("Connection {ConnectionId}: Cleanup completed", connectionId);
    }
}
