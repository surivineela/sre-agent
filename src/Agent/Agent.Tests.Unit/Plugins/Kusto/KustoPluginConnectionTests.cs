// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Runtime.CompilerServices;
using Agent.Core.Interfaces;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Plugins.Connector;
using Agent.Plugins.Kusto;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Unit.Plugins.Kusto;

public class KustoPluginConnectionTests
{
    private const string _kustoClusterUri = "https://sreagent-conn-test2.westus2.kusto.windows.net";
    private const string _kustoDatabase = "testdb";

    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<KustoClient> _kustoClientLogger;
    private readonly ILogger<KustoPlugin> _pluginLogger;
    private readonly string _kustoClusterName;
    private const int _retryCount = 3;
    private const int _retryDelaySeconds = 5; // 5 second delay between retries

    public KustoPluginConnectionTests()
    {
        // Real console logger factory (no filtering so we can see output if tests run locally)
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddSimpleConsole(o =>
                {
                    o.IncludeScopes = false;
                    o.SingleLine = true;
                    o.TimestampFormat = "HH:mm:ss ";
                })
                .SetMinimumLevel(LogLevel.Information);
        });

        _kustoClientLogger = _loggerFactory.CreateLogger<KustoClient>();
        _pluginLogger = _loggerFactory.CreateLogger<KustoPlugin>();

        // Derive the cluster short name expected by ExecuteClusterKustoQuery
        var clusterParam = _kustoClusterUri;
        if (clusterParam.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            clusterParam = clusterParam.Substring("https://".Length);
        }
        if (clusterParam.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            clusterParam = clusterParam.Substring("http://".Length);
        }
        // Take host portion before any '/'
        var slashIdx = clusterParam.IndexOf('/');
        if (slashIdx >= 0)
        {
            clusterParam = clusterParam.Substring(0, slashIdx);
        }
        // Remove suffix if present
        clusterParam = clusterParam.Replace(".kusto.windows.net", string.Empty, StringComparison.OrdinalIgnoreCase);
        _kustoClusterName = clusterParam;
    }

    /// <summary>
    /// Creates a fully wired <see cref="KustoPlugin"/> instance using:
    ///  - Real console loggers
    ///  - A real <see cref="KustoClient"/> whose auth layer returns <see cref="DefaultAzureCredential"/>
    ///  - A mocked <see cref="IAgentOutboundCommunicationService"/>
    /// NOTE: This does NOT execute any live Kusto query yet; individual tests will decide if they should run
    /// live by checking env flags (added in later commits when live tests are implemented).
    /// </summary>
    private (KustoPlugin plugin, KustoClient client, Mock<IAgentOutboundCommunicationService> outboundMock) CreatePlugin()
    {
        // Build minimal Kusto connector settings. For connection tests we'll inject cluster details via env vars
        var connector = new KustoConnector
        {
            Name = "test-kusto",
            Type = "kusto",
            Enabled = true,
            ClusterUrl = _kustoClusterUri,
            Database = _kustoDatabase,
            Auth = new ConnectorAuthSettings
            {
                AuthenticationType = ConnectorAuthType.User // Treat as user / default credential for test purposes
            },
            RegionalClusterGroups = new List<KustoRegionalGroupSettings>
            {
                new() {
                    Name = "default",
                    Regions =
                    {
                        new KustoCluster
                        {
                            Region = "eastus",
                            ClusterUri = _kustoClusterUri,
                            Database = _kustoDatabase
                        }
                    }
                }
            }
        };

        // Auth service mock that returns DefaultAzureCredential for any data connector credential request
        var authService = new Mock<IAuthenticationService>();
        var defaultCredential = new DefaultAzureCredential();
        authService
            .Setup(a => a.GetDataConnectorCredential(It.IsAny<ConnectorAuthSettings>()))
            .Returns(defaultCredential);

        // Outbound communication service mock (no-op behaviors; verify later if needed)
        var outboundMock = new Mock<IAgentOutboundCommunicationService>();
        // For simplicity most outbound methods are stubbed; expand as needed by future tests
        outboundMock
            .Setup(o => o.UpdateThreadWithAgentMessageAsync(It.IsAny<Guid?>(), It.IsAny<ChatMessage>(), It.IsAny<Guid?>(), It.IsAny<StreamMessageType?>()))
            .Returns(Task.CompletedTask);
        outboundMock
            .Setup(o => o.HandleAgentTaskKustoResult(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var kustoClient = new KustoClient(_kustoClientLogger, connector, authService.Object);
        var plugin = new KustoPlugin(_pluginLogger, kustoClient, outboundMock.Object);

        return (plugin, kustoClient, outboundMock);
    }

    [Fact(Skip = "disabled")]
    public async Task ExecuteClusterKustoQuery_PrintOne_ReturnsResultOrSkips()
    {
        var (plugin, _, outboundMock) = CreatePlugin();

        // Act: attempt a lightweight cluster query (retries handled internally)
        var result = await WithRetryAsync(
            () => plugin.ExecuteClusterKustoQuery(_kustoClusterName, _kustoDatabase, "print 1"),
            maxRetries: _retryCount,
            delay: TimeSpan.FromSeconds(_retryDelaySeconds),
            onRetry: async (attempt, ex) =>
            {
                Console.WriteLine($"Retry attempt {attempt} failed: {ex.Message}");
                await Task.CompletedTask;
            });
        Console.WriteLine($"Kusto query result: {result}");
        // Assert: result should contain the implicit column name produced by 'print 1'
        Assert.Contains("print_0", result, StringComparison.OrdinalIgnoreCase);
        // Ensure outbound message was attempted (printed query path true by default)
        outboundMock.Verify(o => o.UpdateThreadWithAgentMessageAsync(It.IsAny<Guid?>(), It.IsAny<ChatMessage>(), It.IsAny<Guid?>(), It.IsAny<StreamMessageType?>()), Times.AtLeastOnce());
    }

    [Fact(Skip = "disabled")]
    public async Task ExecuteKustoQuery_PrintOne_ReturnsResult()
    {
        var (plugin, _, _) = CreatePlugin();
        var result = await WithRetryAsync(
            () => plugin.ExecuteKustoQuery(AzureRegion.EastUS, "print 1"),
            maxRetries: _retryCount,
            delay: TimeSpan.FromSeconds(_retryDelaySeconds),
            onRetry: async (attempt, ex) =>
            {
                Console.WriteLine($"ExecuteKustoQuery retry {attempt} failed: {ex.Message}");
                await Task.CompletedTask;
            });
        Assert.Contains("print_0", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "disabled")]
    public async Task ListFunctionsAsync_ReturnsList()
    {
        var (plugin, _, _) = CreatePlugin();
        var funcs = await WithRetryAsync(
            () => plugin.ListFunctionsAsync(AzureRegion.EastUS),
            maxRetries: _retryCount,
            delay: TimeSpan.FromSeconds(_retryDelaySeconds),
            onRetry: async (attempt, ex) =>
            {
                Console.WriteLine($"ListFunctionsAsync retry {attempt} failed: {ex.Message}");
                await Task.CompletedTask;
            });
        Assert.True(funcs.Count > 0);
    }

    [Fact(Skip = "disabled")]
    public async Task ExecuteFunctionAsync_TestFunc_ReturnsTest()
    {
        var (plugin, _, _) = CreatePlugin();
        var result = await WithRetryAsync(
            () => plugin.ExecuteFunctionAsync("TestFunc", AzureRegion.EastUS),
            maxRetries: _retryCount,
            delay: TimeSpan.FromSeconds(_retryDelaySeconds),
            onRetry: async (attempt, ex) =>
            {
                Console.WriteLine($"ExecuteFunctionAsync(TestFunc) retry {attempt} failed: {ex.Message}");
                await Task.CompletedTask;
            });
        Assert.Contains("test", result, StringComparison.OrdinalIgnoreCase);
    }

    // Common retry helper (generic)
    private static async Task<T> WithRetryAsync<T>(Func<Task<T>> action, int maxRetries = 3, TimeSpan? delay = null, Func<int, Exception, Task>? onRetry = null, CancellationToken cancellationToken = default, [CallerMemberName] string? testName = null)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (maxRetries < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetries));
        }

        var retryDelay = delay ?? TimeSpan.FromSeconds(1);

        var attempt = 0;
        while (attempt < maxRetries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                attempt++;
                return await action();
            }
            catch (Exception ex) when (attempt < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine($"WithRetryAsync attempt {attempt} failed: {ex.Message}");
                if (onRetry != null)
                {
                    await onRetry(attempt, ex);
                }
                await Task.Delay(retryDelay, cancellationToken);
            }
            catch (Exception finalEx) when (attempt >= maxRetries)
            {
                var message = $"{testName} WithRetryAsync giving up after {attempt} attempts";
                Console.WriteLine($"{message}: {finalEx.Message}");
                throw new Exception(message, finalEx);
            }
        }

        return default(T)!; // Should not reach here
    }
}
