// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agent.Runtime.Services.Mcp;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit.Abstractions;

namespace Agent.Tests.Integration.Services.Mcp;

/// <summary>
/// Integration tests for McpSessionWebsocketClient that actually start the MCP Proxy Server
/// and test end-to-end functionality.
/// </summary>
public class SessionWebsocketClientIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly ILoggerFactory _loggerFactory;
    private Process? _proxyServerProcess;
    private int _proxyServerPort;
    private string _proxyServerUrl = string.Empty;

    public SessionWebsocketClientIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    }

    /// <summary>
    /// Start the MCP Proxy Server before running tests.
    /// </summary>
    public async Task InitializeAsync()
    {
        _proxyServerPort = GetAvailablePort();
        _proxyServerUrl = $"ws://localhost:{_proxyServerPort}/mcp/run";

        _output.WriteLine($"Starting Session Proxy Server on port {_proxyServerPort}...");

        // Get the path to the Session.Proxy project (going up from bin/Debug/net9.0)
        var testAssemblyPath = typeof(SessionWebsocketClientIntegrationTests).Assembly.Location;
        var testProjectDir = Path.GetDirectoryName(testAssemblyPath)!;
        var agentDir = Path.GetFullPath(Path.Combine(testProjectDir, "..", "..", "..", ".."));
        var proxyServerProject = Path.Combine(agentDir, "Session.Proxy", "Session.Proxy.csproj");

        _output.WriteLine($"Proxy server project path: {proxyServerProject}");

        if (!File.Exists(proxyServerProject))
        {
            throw new FileNotFoundException($"Session Proxy Server project not found at: {proxyServerProject}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{proxyServerProject}\" --urls http://localhost:{_proxyServerPort}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _proxyServerProcess = new Process { StartInfo = startInfo };

        _proxyServerProcess.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _output.WriteLine($"[SERVER OUT] {e.Data}");
            }
        };

        _proxyServerProcess.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _output.WriteLine($"[SERVER ERR] {e.Data}");
            }
        };

        _proxyServerProcess.Start();
        _proxyServerProcess.BeginOutputReadLine();
        _proxyServerProcess.BeginErrorReadLine();

        // Wait for server to be ready
        await WaitForServerReady(_proxyServerPort);
        _output.WriteLine("Session Proxy Server is ready!");
    }

    /// <summary>
    /// Stop the Session Proxy Server after tests complete.
    /// </summary>
    public Task DisposeAsync()
    {
        if (_proxyServerProcess != null && !_proxyServerProcess.HasExited)
        {
            _output.WriteLine("Stopping Session Proxy Server...");
            _proxyServerProcess.Kill(true);
            _proxyServerProcess.WaitForExit(5000);
            _proxyServerProcess.Dispose();
        }

        _loggerFactory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ConnectAsync_WithValidMcpServer_Succeeds()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<SessionWebsocketClientTransport>();
        var options = new SessionWebsocketClientOptions
        {
            ServerUrl = _proxyServerUrl,
            Command = "npx",
            Arguments = new[] { "-y", "@modelcontextprotocol/server-everything" }
        };

        var clientTransport = new SessionWebsocketClientTransport(options, logger);

        try
        {
            // Act
            var transport = await clientTransport.ConnectAsync();

            // Assert
            Assert.NotNull(transport);
            _output.WriteLine("✓ Connection successful");

            await transport.DisposeAsync();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Connection failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task FullMcpProtocol_InitializeAndListTools_WorksEndToEnd()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<SessionWebsocketClientTransport>();
        var options = new SessionWebsocketClientOptions
        {
            ServerUrl = _proxyServerUrl,
            Command = "npx",
            Arguments = new[] { "-y", "@modelcontextprotocol/server-everything" },
            Name = "E2ETest"
        };

        var clientTransport = new SessionWebsocketClientTransport(options, logger);

        try
        {
            // Act - Create MCP client with the transport
            _output.WriteLine("Creating MCP client...");
            var mcpOptions = new McpClientOptions
            {
                ClientInfo = new()
                {
                    Name = "IntegrationTestClient",
                    Version = "1.0.0"
                }
            };

            var client = await McpClient.CreateAsync(clientTransport, mcpOptions, _loggerFactory);

            // Assert - Verify connection and server info
            Assert.NotNull(client.ServerInfo);
            _output.WriteLine($"✓ Connected to server: {client.ServerInfo.Name}");
            _output.WriteLine($"  Version: {client.ServerInfo.Version}");

            // Act - List tools
            _output.WriteLine("Listing tools...");
            var tools = await client.ListToolsAsync();

            // Assert - Should have some tools
            Assert.NotEmpty(tools);
            _output.WriteLine($"✓ Found {tools.Count()} tools:");
            foreach (var tool in tools.Take(5))
            {
                _output.WriteLine($"  - {tool.Name}: {tool.Description}");
            }

            // Clean up
            if (client is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task SendAndReceive_Messages_WorkCorrectly()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<SessionWebsocketClientTransport>();
        var options = new SessionWebsocketClientOptions
        {
            ServerUrl = _proxyServerUrl,
            Command = "npx",
            Arguments = new[] { "-y", "@modelcontextprotocol/server-everything" }
        };

        var clientTransport = new SessionWebsocketClientTransport(options, logger);
        using var cts = new CancellationTokenSource();

        try
        {
            // Act - Connect
            var transport = (SessionWebsocketTransport)await clientTransport.ConnectAsync();

            // Start reading messages
            var receivedMessages = new List<JsonRpcMessage>();
            var readTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var message in transport.MessageReader.ReadAllAsync(cts.Token))
                    {
                        receivedMessages.Add(message);
                        _output.WriteLine($"Received: {message.GetType().Name}");

                        // Stop after receiving the initialize response
                        if (message is JsonRpcResponse)
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when test completes
                }
            }, cts.Token);

            // Act - Send initialize message
            var initRequest = new JsonRpcRequest
            {
                Id = new RequestId(1),
                Method = "initialize",
                Params = JsonNode.Parse(JsonSerializer.Serialize(new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new { },
                    clientInfo = new
                    {
                        name = "TestClient",
                        version = "1.0.0"
                    }
                }))
            };

            _output.WriteLine("Sending initialize message...");
            await transport.SendMessageAsync(initRequest);

            // Wait for response
            var completedTask = await Task.WhenAny(readTask, Task.Delay(10000));

            if (completedTask != readTask)
            {
                _output.WriteLine("Warning: Test timed out waiting for response");
            }

            // Cancel and wait for cleanup
            cts.Cancel();
            try
            {
                await readTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            // Assert
            Assert.NotEmpty(receivedMessages);
            var response = receivedMessages.OfType<JsonRpcResponse>().FirstOrDefault();
            Assert.NotNull(response);
            _output.WriteLine("✓ Received initialize response");

            await transport.DisposeAsync();
        }
        finally
        {
            cts.Cancel();
        }
    }

    [Fact]
    public async Task MultipleConnections_CanRunConcurrently()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<SessionWebsocketClientTransport>();

        var tasks = new List<Task>();
        for (int i = 0; i < 3; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var options = new SessionWebsocketClientOptions
                {
                    ServerUrl = _proxyServerUrl,
                    Command = "npx",
                    Arguments = new[] { "-y", "@modelcontextprotocol/server-everything" },
                    Name = $"Concurrent-{index}"
                };

                var clientTransport = new SessionWebsocketClientTransport(options, logger);
                try
                {
                    _output.WriteLine($"[{index}] Connecting...");
                    var transport = await clientTransport.ConnectAsync();
                    _output.WriteLine($"[{index}] ✓ Connected");

                    await Task.Delay(1000); // Hold connection briefly

                    await transport.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"[{index}] Failed: {ex.Message}");
                    throw;
                }
                finally
                {
                    _output.WriteLine($"[{index}] Disconnected");
                }
            }));
        }

        // Act & Assert
        await Task.WhenAll(tasks);
        _output.WriteLine("✓ All concurrent connections succeeded");
    }

    [Fact]
    public async Task Dispose_WhileConnected_CleansUpProperly()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<SessionWebsocketClientTransport>();
        var options = new SessionWebsocketClientOptions
        {
            ServerUrl = _proxyServerUrl,
            Command = "npx",
            Arguments = new[] { "-y", "@modelcontextprotocol/server-everything" }
        };

        var clientTransport = new SessionWebsocketClientTransport(options, logger);

        // Act - Connect and immediately dispose
        var transport = await clientTransport.ConnectAsync();
        await transport.DisposeAsync();

        // Assert - Should not throw
        _output.WriteLine("✓ Dispose while connected succeeded");

        // Verify multiple dispose calls work
        await transport.DisposeAsync();
        _output.WriteLine("✓ Multiple dispose calls succeeded");
    }

    /// <summary>
    /// Gets an available TCP port for the proxy server.
    /// </summary>
    private static int GetAvailablePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)socket.LocalEndPoint!).Port;
        return port;
    }

    /// <summary>
    /// Waits for the server to be ready by attempting TCP connections.
    /// </summary>
    private async Task WaitForServerReady(int port, int maxAttempts = 10)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync("localhost", port);
                return;
            }
            catch
            {
                await Task.Delay(1000);
            }
        }

        throw new TimeoutException($"Server did not start within {maxAttempts} seconds");
    }
}
