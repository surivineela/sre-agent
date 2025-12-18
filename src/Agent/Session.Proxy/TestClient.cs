using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Agent.Core.Models.Session;
using Azure.Core;
using Azure.Identity;

namespace Session.Proxy;

/// <summary>
/// Simple test client for the MCP Proxy Server.
/// Run with: dotnet run TestClient [server-url] [command] [args...]
/// Example: dotnet run TestClient ws://localhost:5000/run npx -y @azure-devops/mcp@next msazure
/// </summary>
public static class TestClient
{
    public static async Task Run(string[] args)
    {
        Console.WriteLine("MCP Proxy Test Client");
        Console.WriteLine("=====================");
        Console.WriteLine();

        if (args.Length < 2)
        {
            PrintUsage();
            return;
        }

        // Parse --session flag and --env flags
        bool useSession = false;
        var envVars = new Dictionary<string, string>();
        var argsList = args.ToList();

        for (int i = argsList.Count - 1; i >= 0; i--)
        {
            if (argsList[i] == "--session")
            {
                useSession = true;
                argsList.RemoveAt(i);
            }
            else if (argsList[i] == "--env" && i + 1 < argsList.Count)
            {
                var envValue = argsList[i + 1];
                var parts = envValue.Split('=', 2);
                if (parts.Length == 2)
                {
                    envVars[parts[0]] = parts[1];
                    argsList.RemoveAt(i + 1);
                    argsList.RemoveAt(i);
                }
            }
        }

        if (argsList.Count < 2)
        {
            PrintUsage();
            return;
        }

        string serverUrl = argsList[0];
        string command = argsList[1];
        string[] commandArgs = argsList.Skip(2).ToArray();

        Console.WriteLine($"Server: {serverUrl}");
        Console.WriteLine($"Command: {command}");
        Console.WriteLine($"Args: {string.Join(" ", commandArgs)}");
        Console.WriteLine($"Session mode: {useSession}");
        if (envVars.Count > 0)
        {
            Console.WriteLine($"Environment variables: {string.Join(", ", envVars.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }
        Console.WriteLine();

        try
        {
            await ConnectAndProxy(serverUrl, command, commandArgs, envVars, useSession);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run TestClient [--session] [--env KEY=VALUE ...] <server-url> <command> [args...]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --session         Use Azure session mode (gets token and adds identifier)");
        Console.WriteLine("  --env KEY=VALUE   Set environment variable (can be specified multiple times)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run TestClient ws://localhost:5000/mcp/run npx -y @modelcontextprotocol/server-everything");
        Console.WriteLine("  dotnet run TestClient --session wss://<session-pool-hostname>/mcp/run npx -y @modelcontextprotocol/server-everything");
        Console.WriteLine("  dotnet run TestClient --env API_KEY=secret --env DEBUG=true ws://localhost:5000/mcp/run node server.js");
    }

    private static async Task ConnectAndProxy(string serverUrl, string command, string[] commandArgs, Dictionary<string, string> envVars, bool useSession)
    {
        // Get Azure token if session mode is enabled
        string? token = null;
        if (useSession)
        {
            Console.WriteLine("Acquiring Azure token for scope: https://dynamicsessions.io");
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(new[] { "https://dynamicsessions.io/.default" });
            var tokenResult = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);
            token = tokenResult.Token;
            Console.WriteLine("Token acquired successfully");
            Console.WriteLine();
        }

        // Build the WebSocket URL (no query parameters for connection request)
        var fullUrl = serverUrl;

        using var ws = new ClientWebSocket();
        ws.Options.CollectHttpResponseDetails = true;

        using var cts = new CancellationTokenSource();

        // Add authorization header and session identifier if session mode is enabled
        if (useSession && token != null)
        {
            ws.Options.SetRequestHeader("Authorization", $"Bearer {token}");
            var identifier = Guid.NewGuid().ToString();
            fullUrl += $"?identifier={identifier}";
        }

        Console.WriteLine($"Connecting to: {fullUrl}");
        Console.WriteLine();

        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\nShutting down...");
        };

        try
        {
            await ws.ConnectAsync(new Uri(fullUrl), cts.Token);

            // Check HTTP status code from the WebSocket handshake
            var statusCode = ws.HttpStatusCode;
            Console.WriteLine($"HTTP Status: {(int)statusCode} ({statusCode})");

            if (statusCode != System.Net.HttpStatusCode.SwitchingProtocols)
            {
                Console.Error.WriteLine("WebSocket handshake failed.");
                return;
            }

            Console.WriteLine("Connected!");

            // Send connection request as the first message
            var connectionRequest = new McpConnectionRequest
            {
                Command = command,
                Arguments = commandArgs,
                EnvironmentVariables = envVars.Count > 0 ? envVars : null,
                ProtocolVersion = 2  // Use protocol v2
            };

            var requestJson = JsonSerializer.Serialize(connectionRequest);
            Console.WriteLine($"Sending connection request: {requestJson}");
            await SendMessage(ws, requestJson, cts.Token);

            // Read the first message (should be "ok" or error)
            var firstMessage = await ReceiveMessage(ws, cts.Token);

            if (firstMessage == null)
            {
                Console.Error.WriteLine("Connection closed by server immediately after connecting.");
                return;
            }

            Console.WriteLine($"Server response: {firstMessage}");

            if (!firstMessage.Equals("ok", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Starting bidirectional proxy. Type messages and press Enter to send.");
            Console.WriteLine("Press Ctrl+C to exit.");
            Console.WriteLine("---");
            Console.WriteLine();

            // Start receiving task
            var receiveTask = Task.Run(async () =>
            {
                try
                {
                    while (ws.State == WebSocketState.Open && !cts.Token.IsCancellationRequested)
                    {
                        var message = await ReceiveMessage(ws, cts.Token);
                        if (message != null)
                        {
                            // Parse channel indicator for v2 protocol
                            if (message.Length >= 2 && (message[0] == McpProxyProtocol.ChannelStdout || message[0] == McpProxyProtocol.ChannelStderr))
                            {
                                char channelChar = message[0];
                                string content = message.Substring(1);

                                if (channelChar == McpProxyProtocol.ChannelStderr)
                                {
                                    // Display stderr in red
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.Error.WriteLine($"<< [STDERR] {content}");
                                    Console.ResetColor();
                                }
                                else
                                {
                                    // Display stdout normally
                                    Console.WriteLine($"<< {content}");
                                }
                            }
                            else
                            {
                                // No channel indicator (e.g., handshake) - display normally
                                Console.WriteLine($"<< {message}");
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                }
                catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    Console.WriteLine("Connection closed by server");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Receive error: {ex.Message}");
                }
            }, cts.Token);

            // Send task - read from console and send to WebSocket
            var sendTask = Task.Run(async () =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        var line = Console.ReadLine();
                        if (line == null || cts.Token.IsCancellationRequested)
                        {
                            break;
                        }

                        if (!string.IsNullOrEmpty(line))
                        {
                            await SendMessage(ws, line, cts.Token);
                            Console.WriteLine($">> {line}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Send error: {ex.Message}");
                }
            }, cts.Token);

            // Wait for either task to complete
            await Task.WhenAny(receiveTask, sendTask);

            // Cancel both tasks
            cts.Cancel();

            // Close WebSocket gracefully
            if (ws.State == WebSocketState.Open)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
            }

            // Wait for tasks to complete
            try
            {
                await Task.WhenAll(receiveTask, sendTask);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            Console.WriteLine("Connection closed.");
        }
        catch (WebSocketException ex)
        {
            Console.Error.WriteLine($"WebSocket error: {ex.Message}");
            throw;
        }
    }

    private static async Task<string?> ReceiveMessage(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();

        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        ms.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(ms, Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static async Task SendMessage(ClientWebSocket ws, string message, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        await ws.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }
}
