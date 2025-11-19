using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
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

        // Parse --session flag
        bool useSession = false;
        var argsList = args.ToList();
        if (argsList.Contains("--session"))
        {
            useSession = true;
            argsList.Remove("--session");
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
        Console.WriteLine();

        try
        {
            await ConnectAndProxy(serverUrl, command, commandArgs, useSession);
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
        Console.WriteLine("  dotnet run TestClient [--session] <server-url> <command> [args...]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --session    Use Azure session mode (gets token and adds identifier)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run TestClient ws://localhost:5000/run npx -y @modelcontextprotocol/server-everything");
        Console.WriteLine("  dotnet run TestClient --session wss://<session-pool-hostname>/run npx -y @modelcontextprotocol/server-everything");
    }

    private static async Task ConnectAndProxy(string serverUrl, string command, string[] commandArgs, bool useSession)
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

        // Build the WebSocket URL with query parameters
        var argsJson = JsonSerializer.Serialize(commandArgs);
        var encodedArgs = Uri.EscapeDataString(argsJson);
        var encodedCmd = Uri.EscapeDataString(command);
        var fullUrl = $"{serverUrl}?cmd={encodedCmd}&args={encodedArgs}";

        using var ws = new ClientWebSocket();
        ws.Options.CollectHttpResponseDetails = true;

        using var cts = new CancellationTokenSource();

        // Add authorization header if session mode is enabled
        if (useSession && token != null)
        {
            ws.Options.SetRequestHeader("Authorization", $"Bearer {token}");
            var identifier = Guid.NewGuid().ToString();
            fullUrl += $"&identifier={identifier}";
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
                            Console.WriteLine($"<< {message}");
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
