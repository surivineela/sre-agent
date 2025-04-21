using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Agent.Tests.Common;
using Agent.Tests.Common.Mocks;
using Agent.Tests.Common.ScenarioTestHelpers;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agent.Evals;

[TestClass]
public class TlsBestPracticesEvals
{
    public TestContext TestContext { get; set; }

    private IHost _host;
    private ChatConfiguration _chatConfiguration;
    private string? _llmDeploymentName;
    private BasicMockSetup _mocks;
    private static int _iterationCount = 1;

    private const string BaseResourceId = "/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/my-resource-group/providers/Microsoft.Web/sites";
    private DurableTaskClient _durableTaskClient;
    private TlsBestPracticeAgentFactory _agentFactory;

    private List<TlsStatus> _testApps = new List<TlsStatus>
    {
        new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app1", ResourceId : $"{BaseResourceId}/app1", Location:"eastus" ),
        new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app2", ResourceId : $"{BaseResourceId}/app2", Location:"eastus" ),
        new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app3", ResourceId : $"{BaseResourceId}/app3", Location:"eastus" ),
        new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app4", ResourceId : $"{BaseResourceId}/app4", Location:"eastus" ),
        new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app5", ResourceId : $"{BaseResourceId}/app5", Location:"eastus" ),
    };

    [TestInitialize]
    public async Task TestInitialize()
    {
        var builder = TestHelpers.BuildTestApp(out _llmDeploymentName);
        builder.RegisterDefaultServices();
        builder.ConfigureDurable();

        _mocks = new BasicMockSetup(DateTimeOffset.Parse("2025-02-24T01:00:00Z"), null);
        _mocks.ArmPlugin.ConfigureTlsStatus(_testApps.ToDictionary(x => x.ResourceId));

        var services = builder.Services;
        services.AddMockServices(_mocks);
        TlsTestHelpers.AddPluginDefinitions(services);
        services.AddSingleton<ToolsRepository>();
        services.AddSingleton<TlsBestPracticeAgentFactory>();

        var sp = services.BuildServiceProvider();
        _durableTaskClient = sp.GetRequiredService<DurableTaskClient>();
        _agentFactory = sp.GetRequiredService<TlsBestPracticeAgentFactory>();
        _host = builder.Build();

        IChatClient client = _host.Services.GetRequiredService<IChatClient>();
        IEvaluationTokenCounter? tokenCounter = null;
        _chatConfiguration = new ChatConfiguration(client, tokenCounter);

        await _host.StartAsync();
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    private static IEnumerable<object[]> TestData_Iterations()
    {
        for (int i = 0; i < _iterationCount; i++)
        {
            yield return new object[] { $"Iteration: {i}" };
        }
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task UpdateHealthyApps(string iteration)
    {
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMinutes(5));

        EvalInput evalInput = new EvalInput(_chatConfiguration, this.TestContext, _llmDeploymentName);
        evalInput.GroundedContext = """
            ## Ground Truth:
            1. Recieve the list of applications that need to be updated to the specified TLS version
            2. Request and wait for an approval
            3. Perform the updates one by one, monitoring health for 30 seconds before moving to the next app
            4. All applications should be updated to the specified TLS version.
            5. Acknowledge that the update is complete.

            ## Expected Response Characteristics
            - The agent should keep the user informed as it performs each step of the update
            - The responses should make good use of emoji, be brief but informative.
            - The response should avoid unnecessary information or ambiguity.
            """;

        evalInput.ExampleResponse = """
            Here are several examples of good responses for a few different steps of the process:

            ## Example 1

            ✅ TLS version update completed for app5 at 2025-02-24T01:00:00. No anomalies detected.

            ## Example 2

            ▶️ TLS version update initiated for app1 at 2025-02-24T01:00:00.0000000Z

            ## Example 3

            - **app1**: ✅ TLS version updated to 1.2 successfully at 2025-02-24T01:00:00.
            - **app2**: ✅ TLS version updated to 1.2 successfully at 2025-02-24T01:00:30.
            - **app3**: ✅ TLS version updated to 1.2 successfully at 2025-02-24T01:01:00.
            - **app4**: ✅ TLS version updated to 1.2 successfully at 2025-02-24T01:01:30.
            - **app5**: ✅ TLS version updated to 1.2 successfully at 2025-02-24T01:02:00.

            The update was completed successfully without any issues. 🎉
            """;

        var agentInput = new TlsBestPracticesInput { AppsInViolation = _testApps, DesiredVersion = "1.2", };
        string? instanceID = "";

        try
        {
            instanceID = await _agentFactory.StartOrchestration(agentInput, Guid.NewGuid());

            await ApprovalTestHelper.DoApproval(
                _durableTaskClient,
                _mocks.TimeProvider,
                instanceID,
                null, // seriously MSTest, why don't you have an ILogger?
                tokenSource.Token);

            OrchestrationMetadata orchestrationMetadata = await _durableTaskClient.WaitForInstanceCompletionWithRetryAsync(instanceID, tokenSource.Token);
            Assert.IsTrue(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed);

            var fullHistory = orchestrationMetadata.ReadChatHistory();
            await evalInput.EvaluateAgentResponsesAsync(fullHistory);

            foreach (var app in _testApps)
            {
                TestContext.WriteLine($"Test complete. App {app.Name} is now set to TLS {_mocks.ArmPlugin.GetTlsStatus(app.ResourceId)}");
            }

            foreach (var app in _testApps)
            {
                Assert.AreEqual("1.2", _mocks.ArmPlugin.GetTlsStatus(app.ResourceId), ignoreCase: true, $"App {app.Name} does not have expected TLS setting.");
            }
        }
        catch (Grpc.Core.RpcException ex)
        {
            Assert.Fail($"Make sure you have the DTS emulator running (run-durable-emulator.ps1) or your appsettings.development.json has a valid Durable Task Scheduler connection string.{Environment.NewLine} {ex}");
        }
        catch (TaskCanceledException)
        {
            if (!string.IsNullOrEmpty(instanceID))
            {
                await _durableTaskClient.TerminateInstanceAsync(instanceID, new TerminateInstanceOptions { Output = "test cleanup", Recursive = true });
            }
            Assert.Fail("Orchestration timed out");
        }
    }

}
