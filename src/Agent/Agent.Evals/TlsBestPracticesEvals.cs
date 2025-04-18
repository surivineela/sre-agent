using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Mocks;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using Agent.Runtime.SubAgents.Core;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Agent.Tests.Common;
using Agent.Tests.Common.Mocks;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.AzureManaged;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.AzureManaged;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Agent.Evals;
[TestClass]
public class TlsBestPracticesEvals
{
    public TestContext TestContext { get; set; }

    private IHost _host;
    private ChatConfiguration _chatConfiguration;

    private static int _iterationCount = 1;

    private TimeProvider _timeProvider;
    private DurableTaskClient _durableTaskClient;
    private TlsBestPracticeAgentFactory _agentFactory;
    private const string BaseResourceId = "/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/my-resource-group/providers/Microsoft.Web/sites";

    private MockApprovalPlugin _mockApprovalPlugin;
    private MockRecordActionsPlugin _mockRecordActionsPlugin;
    private MockArmPlugin _mockArmPlugin;
    private MockMetricsPlugin _mockMetricsPlugin;
    private MockTimePlugin _mockTimePlugin;
    private MockCommunicationService _mockCommunicationService;
    private string? _llmDeploymentName;

    private List<TlsStatus> _testApps = new List<TlsStatus>
    {
        new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app1", ResourceId : $"{BaseResourceId}/app1", Location:"eastus" ),
        new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app2", ResourceId : $"{BaseResourceId}/app2", Location:"eastus" ),
        new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app3", ResourceId : $"{BaseResourceId}/app3", Location:"eastus" ),
        new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app4", ResourceId : $"{BaseResourceId}/app4", Location:"eastus" ),
        new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app5", ResourceId : $"{BaseResourceId}/app5", Location:"eastus" ),
    };


    // Static constructor to initialize _iterationCount
    static TlsBestPracticesEvals()
    {
        // Retrieve the IterationCount from environment variables or a default value
        string iterationCountEnv = Environment.GetEnvironmentVariable("IterationCount");
        if (int.TryParse(iterationCountEnv, out int parsedIterations))
        {
            Console.WriteLine($"Static Constructor: IterationCount is {parsedIterations}");
            _iterationCount = parsedIterations;
        }
        else
        {
            Console.WriteLine("TlsBestPracticesEvals Static Constructor: IterationCount not found or invalid. Using default value.");
        }
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        var builder = TestHelpers.BuildTestApp(out _llmDeploymentName);
        var services = builder.Services;

        _timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2025-02-24T01:00:00Z"));
        _mockApprovalPlugin = new MockApprovalPlugin();
        _mockArmPlugin = new MockArmPlugin(_timeProvider, _mockApprovalPlugin);
        _mockArmPlugin.ConfigureTlsStatus(_testApps.ToDictionary(x => x.ResourceId));
        _mockMetricsPlugin = new MockMetricsPlugin(_timeProvider);
        _mockTimePlugin = new MockTimePlugin(_timeProvider);
        _mockCommunicationService = new MockCommunicationService(logger: null);
        _mockRecordActionsPlugin = new MockRecordActionsPlugin(_timeProvider, logger: null);

        services.AddSingleton<TimeProvider>(_timeProvider);
        services.AddSingleton<IApprovalPlugin>(_mockApprovalPlugin);
        services.AddSingleton<IRecordActionsPlugin>(_mockRecordActionsPlugin);
        services.AddSingleton<IArmPlugin>(_mockArmPlugin);
        services.AddSingleton<IMetricsPlugin>(_mockMetricsPlugin);
        services.AddSingleton<ITimePlugin>(_mockTimePlugin);
        services.AddSingleton<IAgentOutboundCommunicationService>(_mockCommunicationService);

        services.AddSingleton<MetricsPluginDefinition>();
        services.AddSingleton<ArmPluginDefinition>();
        services.AddSingleton<RecordActionsPluginDefinition>();
        services.AddSingleton<ControlFlowPluginDefinition>();
        services.AddSingleton<ApprovalPluginDefinition>();

        services.AddSingleton<IThreadOrchestrationManager, InMemoryThreadOrchestrationManager>();
        services.AddSingleton<ToolsRepository>();

        services.AddSingleton<TlsBestPracticeAgentFactory>();

        string durableConnectionString = builder.ResolveDtsConnectionString();

        services.AddDurableTaskWorker(durableBuilder =>
        {
            durableBuilder.AddTasks(r =>
            {
                DurableHelper.AddAllGeneratedTasks(r);
            });

            durableBuilder.UseDurableTaskScheduler(durableConnectionString);
        });

        services.AddDurableTaskClient(durableBuilder =>
        {
            durableBuilder.UseDurableTaskScheduler(durableConnectionString);
        });

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
        string groundedContext = """
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

        string exampleResponse = """
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

        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMinutes(5));

        var input = new TlsBestPracticesInput { AppsInViolation = _testApps, DesiredVersion = "1.2", };
        string? instanceID = "";

        try
        {
            instanceID = await _agentFactory.StartOrchestration(input, Guid.NewGuid());

            var (approved, approvalError) = await ApprovalTestHelper.DoApproval(
                _durableTaskClient,
                _timeProvider,
                instanceID,
                null, // seriously MSTest, why don't you have an ILogger?
                tokenSource.Token);

            if (!approved)
            {
                Assert.Fail(approvalError);
            }

            var orchestrationMetadata = await _durableTaskClient.WaitForInstanceCompletionAsync(instanceID, getInputsAndOutputs: true, tokenSource.Token);
            if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
            {
                Assert.Fail(orchestrationMetadata.FailureDetails.ToString());
            }

            Assert.IsTrue(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed);

            var fullHistoryRaw = orchestrationMetadata.ReadCustomStatusAs<string>();
            var fullHistory = System.Text.Json.JsonSerializer.Deserialize<ChatMessage[]>(fullHistoryRaw, new System.Text.Json.JsonSerializerOptions
            {

                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });


            List<ChatMessage> messagesSoFar = new List<ChatMessage>();

            //var messagesToUser = _mockCommunicationService.Messages
            //    .Select(x => new ChatMessage(ChatRole.Assistant, x))
            //    .ToList();

            foreach (var msg in fullHistory)
            {
                messagesSoFar.Add(msg);

                var response = msg switch
                {
                    _ when msg.Role == ChatRole.Assistant && !string.IsNullOrEmpty(msg.Text) => new ChatResponse(msg),
                    _ when msg.Contents.OfType<FunctionCallContent>().SingleOrDefault() is { Name: "NotifyUser" } functionCall =>
                        new ChatResponse(new ChatMessage(ChatRole.Assistant, functionCall.Arguments["message"].ToString())),
                    _ => null
                };

                if (response != null)
                {
                    await response.EvaluateAsync(this.TestContext, this._chatConfiguration, messagesSoFar, groundedContext, exampleResponse, _llmDeploymentName);
                }
            }



            foreach (var app in _testApps)
            {
                TestContext.WriteLine($"Test complete. App {app.Name} is now set to TLS {_mockArmPlugin.GetTlsStatus(app.ResourceId)}");
            }

            foreach (var app in _testApps)
            {
                Assert.AreEqual("1.2", _mockArmPlugin.GetTlsStatus(app.ResourceId), ignoreCase: true, $"App {app.Name} does not have expected TLS setting.");
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
