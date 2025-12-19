using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using Session.Proxy.Services.McpProtocol;

namespace Session.Proxy.Services;

/// <summary>
/// Service that manages WebSocket connections and proxies data between WebSocket clients and MCP server processes.
/// </summary>
public class McpProxyService
{
    private readonly ILogger<McpProxyService> _logger;
    private readonly IdentityProviderClient _identityProviderClient;
    private readonly ProtocolHandlerFactory _protocolHandlerFactory;
    private readonly string _msiEndpoint;
    private static readonly UTF8Encoding NoBomUtf8 = new(encoderShouldEmitUTF8Identifier: false);

    public McpProxyService(ILogger<McpProxyService> logger, IdentityProviderClient identityProviderClient, IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _protocolHandlerFactory = new ProtocolHandlerFactory(loggerFactory);
        _identityProviderClient = identityProviderClient;
        _msiEndpoint = identityProviderClient.MsiEndpoint;
    }

    /// <summary>
    /// Handles a WebSocket connection by launching an MCP server process and proxying data bidirectionally.
    /// </summary>
    public async Task HandleWebSocketConnection(
        WebSocket webSocket,
        string command,
        string[] args,
        Dictionary<string, string>? envVars,
        Dictionary<string, string>? actionTokens,
        int protocolVersion,
        CancellationToken cancellationToken)
    {
        var connectionId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("Connection {ConnectionId}: Starting MCP proxy (protocol v{ProtocolVersion}) for command: {Command} {Args}",
            connectionId, protocolVersion, command, string.Join(" ", args));

        // Create protocol handler for this connection
        var protocolHandler = _protocolHandlerFactory.CreateHandler(protocolVersion);

        if (envVars != null && envVars.Count > 0)
        {
            _logger.LogInformation("Connection {ConnectionId}: Environment variables provided: {Count}",
                connectionId, envVars.Count);
        }

        if (actionTokens != null && actionTokens.Count > 0)
        {
            _logger.LogInformation("Connection {ConnectionId}: Action tokens provided. Token scopes: {Scopes}",
                connectionId, string.Join(", ", actionTokens.Keys));
        }

        Process? process = null;
        try
        {
            // Add action tokens to the token service if provided
            if (actionTokens != null && actionTokens.Count > 0)
            {
                await _identityProviderClient.AddTokensAsync(actionTokens);
                _logger.LogInformation("Connection {ConnectionId}: Action tokens added to token service", connectionId);
            }

            // Launch the MCP server process
            process = LaunchMcpProcess(command, args, envVars, connectionId);

            // Send "ok" message to indicate successful initialization
            await SendWebSocketMessage(webSocket, "ok", cancellationToken);
            _logger.LogInformation("Connection {ConnectionId}: MCP server launched successfully", connectionId);

            // Start bidirectional proxying
            await ProxyBidirectionally(webSocket, process, connectionId, protocolHandler, cancellationToken);
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
    private Process LaunchMcpProcess(string command, string[] args, Dictionary<string, string>? envVars, string connectionId)
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

            // Add MSI endpoint environment variables if action tokens are provided
            startInfo.Environment["IDENTITY_ENDPOINT"] = _msiEndpoint;
            startInfo.Environment["IDENTITY_HEADER"] = connectionId;
            _logger.LogInformation("Connection {ConnectionId}: Setting MSI endpoint environment variables. IDENTITY_ENDPOINT={Endpoint}",
                connectionId, _msiEndpoint);

            // Add environment variables if provided
            if (envVars != null)
            {
                foreach (var kvp in envVars)
                {
                    startInfo.Environment[kvp.Key] = kvp.Value;
                    _logger.LogDebug("Connection {ConnectionId}: Setting environment variable: {Key}",
                        connectionId, kvp.Key);
                }
            }

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
        IProtocolHandler protocolHandler,
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
        var mcpStdoutTask = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Connection {ConnectionId}: MCP process running: {IsRunning}, HasExited: {HasExited}",
                    connectionId, !process.HasExited, process.HasExited);
                await ProxyMcpStdoutToWebSocket(process, webSocket, connectionId, protocolHandler, cts.Token);
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

        // Task to read from MCP server stderr and handle it
        var mcpStderrTask = Task.Run(async () =>
        {
            try
            {
                await ProxyMcpStderrToWebSocket(process, connectionId, webSocket, protocolHandler);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection {ConnectionId}: Error reading stderr", connectionId);
            }
        }, cts.Token);

        // Wait for any proxy task to complete (or fail)
        await Task.WhenAny(wsToMcpTask, mcpStdoutTask, mcpStderrTask);

        // Cancel all tasks
        cts.Cancel();

        // wait for the stderr flush
        await Task.Delay(5000);

        // Wait for all tasks to complete with a timeout
        try
        {
            await Task.WhenAll(wsToMcpTask, mcpStdoutTask, mcpStderrTask).WaitAsync(TimeSpan.FromSeconds(5));
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
    private async Task ProxyMcpStdoutToWebSocket(
        Process process,
        WebSocket webSocket,
        string connectionId,
        IProtocolHandler protocolHandler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Connection {ConnectionId}: Starting to read from MCP stdout", connectionId);
        var stdoutReader = new StreamReader(process.StandardOutput.BaseStream, NoBomUtf8, leaveOpen: false);

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

            // Use protocol handler to format and send message
            try
            {
                await protocolHandler.SendStdoutMessageAsync(webSocket, line, cancellationToken);

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
    private async Task ProxyMcpStderrToWebSocket(
        Process process,
        string connectionId,
        WebSocket webSocket,
        IProtocolHandler protocolHandler)
    {
        // Use StreamReader to handle UTF-8 properly (including multi-byte characters)
        var stderrReader = new StreamReader(process.StandardError.BaseStream, NoBomUtf8, leaveOpen: false);
        var buffer = new char[8192];

        // Read until EOF to ensure we capture all stderr, even after cancellation is requested
        while (true)
        {
            int charsRead;
            try
            {
                // Don't pass cancellationToken to ReadAsync - we want to drain the buffer completely
                charsRead = await stderrReader.ReadAsync(buffer.AsMemory(), CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection {ConnectionId}: Error reading stderr", connectionId);
                break;
            }

            if (charsRead == 0)
            {
                // EOF reached - all stderr has been read
                break;
            }

            var stderrText = new string(buffer, 0, charsRead);
            _logger.LogWarning("Connection {ConnectionId}: MCP stderr: {Stderr}", connectionId, stderrText);

            if (webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    await protocolHandler.SendStderrMessageAsync(webSocket, stderrText, CancellationToken.None);
                    _logger.LogInformation("Connection {ConnectionId}: Sent stderr to WebSocket: {Length}", connectionId, stderrText.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Connection {ConnectionId}: Error sending stderr to WebSocket", connectionId);
                }
            }
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
