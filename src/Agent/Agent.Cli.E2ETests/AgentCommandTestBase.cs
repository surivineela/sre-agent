// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;

namespace Agent.Cli.Tests.E2E;

/// <summary>
/// Base class for E2E tests that execute Agent CLI commands against a mock backend server.
/// Provides automatic setup and teardown of the in-memory test server with data isolation per test.
/// </summary>
public abstract class AgentCommandTestBase : IClassFixture<MockWebApplicationFactory>, IDisposable
{
    protected MockWebApplicationFactory Factory { get; }
    protected CliTestRunner Runner { get; }

    protected AgentCommandTestBase(MockWebApplicationFactory factory)
    {
        Factory = factory;

        // Skip mock server setup when running against real server
        if (!CliTestRunner.UseRealServer)
        {
            // Reset server data for test isolation
            Factory.Reset();

            // Configure ApiService to use the in-memory test HttpClient
            Agent.Cli.Services.ApiService.SetHttpClientFactory(() => factory.CreateClient());

            // Create CLI runner with mock URL for config file (HTTP requests use factory client)
            Runner = new CliTestRunner(mockServerUrl: "http://localhost");
        }
        else
        {
            // When using real server, just create CLI runner (it will configure itself from environment)
            Runner = new CliTestRunner();
        }
    }

    public virtual void Dispose()
    {
        // Only reset HttpClient factory if we set it up (i.e., not using real server)
        if (!CliTestRunner.UseRealServer)
        {
            // Reset HttpClient factory after test
            Agent.Cli.Services.ApiService.SetHttpClientFactory(null);
        }

        // Reset ConsoleUI test injection
        Agent.Cli.Helpers.ConsoleUI.ReadLineHandler = null;

        Runner.Dispose();
        GC.SuppressFinalize(this);
    }
}
