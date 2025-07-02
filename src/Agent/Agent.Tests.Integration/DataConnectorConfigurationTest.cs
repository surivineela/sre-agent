// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Configuration;
using Agent.Runtime.DataConnectors;
using Agent.Tests.Common.TestApplication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace Agent.Tests.Integration;

public class DataConnectorConfigurationTest
{
    private readonly ITestOutputHelper _outputHelper;

    public DataConnectorConfigurationTest(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
    }

    [Theory]
    [MemberData(nameof(TestData_AppSettings))]
    public async Task Test_RunDataConnectors(string configSettings)
    {
        using MemoryStream configStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(configSettings));

        await using AgentTestApp target = new AgentTestApp(_outputHelper, webHostBuilder =>
        {
            webHostBuilder.ConfigureServices(services => services.AddSingleton<TestDataConnectorVerifier>());
            webHostBuilder.ConfigureAppConfiguration( (context, config) => config.AddJsonStream(configStream));
        });

        TestDataConnectorVerifier verifier = target.Services.GetRequiredService<TestDataConnectorVerifier>();

        // the background service that runs the data connectors is asynchronous to app startup
        // so we need to poll and wait for the data connectors to be initialized and run
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!cts.IsCancellationRequested)
        {
            // Wait for the data connectors to be initialized and run
            if (verifier.DataConnectors.Count == verifier.DataConnectorsSettings.Count)
            {
                break;
            }

            await Task.Delay(1000, cts.Token);
        }

        Assert.Equal(verifier.DataConnectorsSettings.Count, verifier.DataConnectors.Count);

        // verify that there is a one-to-one mapping betwen the data connector settings and the data connector instances
        foreach (var connectorSetting in verifier.DataConnectorsSettings)
        {
            // Find the corresponding data connector instance for this setting
            KeyValuePair<IDataConnector, DataConnectorSettings?> matchingConnector = Assert.Single(
                verifier.DataConnectors,
                dc => dc.Value?.Name == connectorSetting.Name);

            Assert.Equal(matchingConnector.Key.GetType().GetCustomAttribute<DataConnectorAttribute>()?.Type, connectorSetting.DataConnectorType, StringComparer.OrdinalIgnoreCase);
        }

        // Verify each connector instance maps to a unique setting
        var usedSettingNames = new HashSet<string>();
        foreach (var connector in verifier.DataConnectors)
        {
            Assert.NotNull(connector.Value);
            Assert.True(usedSettingNames.Add(connector.Value.Name),
                $"Multiple connector instances are using the same settings for {connector.Value.Name}");
        }
    }

    public static IEnumerable<object[]> TestData_AppSettings()
    {
        yield return
        ["""
        {
            "AppSettings" :
            {
                "Core":
                {
                }
            }
        }
        """];

        yield return
        ["""
        {
            "AppSettings" :
            {
                "Core":
                {
                    "DataConnectors": []
                }
            }
        }
        """];

        yield return
        ["""
        {
            "AppSettings" :
            {
                "Core":
                {
                    "DataConnectors": [
                        {
                            "Name": "test1",
                            "DataConnectorType": "TestConnector1",
                            "DataSource": "whatever",
                            "Identity": ""
                        }
                    ]
                }
            }
        }
        """];

        yield return
        ["""
        {
            "AppSettings" :
            {
                "Core":
                {
                    "DataConnectors": [
                        {
                            "Name": "test1",
                            "DataConnectorType": "TestConnector1",
                            "DataSource": "whatever",
                            "Identity": ""
                        },
                        {
                            "Name": "test2",
                            "DataConnectorType": "TestConnector2",
                            "DataSource": "whatever",
                            "Identity": ""
                        }
                    ]
                }
            }
        }
        """];

        yield return
        ["""
        {
            "AppSettings" :
            {
                "Core":
                {
                    "DataConnectors": [
                        {
                            "Name": "test1-a",
                            "DataConnectorType": "TestConnector1",
                            "DataSource": "whatever",
                            "Identity": ""
                        },
                        {
                            "Name": "test1-b",
                            "DataConnectorType": "TestConnector1",
                            "DataSource": "whatever",
                            "Identity": ""
                        },
                        {
                            "Name": "test2",
                            "DataConnectorType": "TestConnector2",
                            "DataSource": "whatever",
                            "Identity": ""
                        }
                    ]
                }
            }
        }
        """];
    }
}

internal class TestDataConnectorVerifier
{
    public List<DataConnectorSettings> DataConnectorsSettings { get; }

    public List<KeyValuePair<IDataConnector, DataConnectorSettings?>> DataConnectors { get; }

    public TestDataConnectorVerifier(IOptions<List<DataConnectorSettings>> options)
    {
        DataConnectorsSettings = options.Value ?? throw new ArgumentNullException(nameof(options));
        DataConnectors = new List<KeyValuePair<IDataConnector, DataConnectorSettings?>>(DataConnectorsSettings.Count);
    }

    public void AddConnector(IDataConnector instance, DataConnectorSettings? settings)
    {
        DataConnectors.Add(new KeyValuePair<IDataConnector, DataConnectorSettings?>(instance, settings));
    }
}

[DataConnector("TestConnector1")]
internal class TestDataConnector1 : IDataConnector
{
    private readonly TestDataConnectorVerifier? _verifier;
    private DataConnectorSettings? _settings;

    public TestDataConnector1(IServiceProvider sp)
    {
        // inject an IServiceProvider instead of TestDataConnectorVerifier directly so that we don't need to add the TestDataConnectorVerifier to the service collection in the test app for all tests.
        _verifier = sp.GetService<TestDataConnectorVerifier>();
    }

    public TimeSpan Interval => TimeSpan.FromSeconds(5);

    public Task InitAsync(DataConnectorSettings settings, CancellationToken stoppingToken)
    {
        _settings = settings;

        return Task.CompletedTask;
    }
    public Task RunAsync(CancellationToken stoppingToken)
    {
        _verifier!.AddConnector(this, _settings);

        return Task.CompletedTask;
    }
}

[DataConnector("TestConnector2")]
internal class TestDataConnector2 : IDataConnector
{
    private readonly TestDataConnectorVerifier? _verifier;
    private DataConnectorSettings? _settings;

    public TestDataConnector2(IServiceProvider sp)
    {
        // inject an IServiceProvider instead of TestDataConnectorVerifier directly so that we don't need to add the TestDataConnectorVerifier to the service collection in the test app for all tests.
        _verifier = sp.GetService<TestDataConnectorVerifier>();
    }

    public TimeSpan Interval => TimeSpan.FromSeconds(5);

    public Task InitAsync(DataConnectorSettings settings, CancellationToken stoppingToken)
    {
        _settings = settings;

        return Task.CompletedTask;
    }
    public Task RunAsync(CancellationToken stoppingToken)
    {
        _verifier!.AddConnector(this, _settings);

        return Task.CompletedTask;
    }
}
