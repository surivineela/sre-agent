using Agent.Core.Interfaces;
using Agent.Core.Models;
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

[DoNotParallelize]
[TestClass]
public class TlsBestPracticesEvals
{
    public TestContext TestContext { get; set; }

    private IHost _host = null!;
    private ChatConfiguration _chatConfiguration = null!;
    private string? _llmDeploymentName;
    private BasicMockSetup _mocks = null!;
    private static int _iterationCount = 1;

    private const string BaseResourceId = "/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/my-resource-group/providers/Microsoft.Web/sites";
    private DurableTaskClient _durableTaskClient = null!;
    private TlsBestPracticeAgentFactory _agentFactory = null!;
    private IThreadRepository _threadRepository = null!;

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
        TlsTestHelpers.AddPluginDefinitionsForGenericSubAgent(services);
        services.AddSingleton<IToolsRepository, ToolsRepository>();
        services.AddSingleton<TlsBestPracticeAgentFactory>();

        _host = builder.Build();

        IChatClient client = _host.Services.GetRequiredService<IChatClient>();
        _durableTaskClient = _host.Services.GetRequiredService<DurableTaskClient>();
        _agentFactory = _host.Services.GetRequiredService<TlsBestPracticeAgentFactory>();
        _threadRepository = _host.Services.GetRequiredService<IThreadRepository>();
        _chatConfiguration = new ChatConfiguration(client);

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
    public async Task Tls_UpdateHealthyApps(string iteration)
    {
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMinutes(5));

        EvalInput evalInput = new EvalInput(_chatConfiguration, this.TestContext, _llmDeploymentName ?? string.Empty);
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
            var threadId = Guid.NewGuid();
            instanceID = await _agentFactory.StartOrchestration(agentInput, threadId);

            var orchestrationMetadata = await ApprovalTestHelper.WaitForCompletionWithAutomaticApprovals(
                durableTaskClient: _durableTaskClient,
                instanceID,
                threadRepository: _threadRepository,
                threadId,
                logger: null,
                tokenSource.Token);

            var fullHistory = orchestrationMetadata.ReadChatHistory();
            await evalInput.EvaluateAgentResponsesAsync(fullHistory, tokenSource.Token);

            foreach (var app in _testApps)
            {
                TestContext.WriteLine($"Test complete. App {app.Name} is now set to TLS {_mocks.ArmPlugin.GetTlsStatus(app.ResourceId)}");
            }

            foreach (var app in _testApps)
            {
                Assert.AreEqual(agentInput.DesiredVersion, _mocks.ArmPlugin.GetTlsStatus(app.ResourceId), ignoreCase: true, $"App {app.Name} does not have expected TLS setting.");
            }
        }
        catch (Grpc.Core.RpcException ex)
        {
            Assert.Fail($"Make sure you have the DTS emulator running (run-durable-emulator.ps1) or your appsettings.development.json has a valid Durable Task Scheduler connection string.{Environment.NewLine} {ex}");
        }
        finally
        {
            if (!string.IsNullOrEmpty(instanceID))
            {
                await _durableTaskClient.TerminateInstanceAsync(instanceID, new TerminateInstanceOptions { Output = "test cleanup", Recursive = true });
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task Tls_RollbackUnhealthyApp(string iteration)
    {
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMinutes(5));

        _mocks.MetricsPlugin.UnhealthyResourceIds.Add(_testApps[1].ResourceId);

        EvalInput evalInput = new EvalInput(_chatConfiguration, this.TestContext, _llmDeploymentName ?? string.Empty);
        evalInput.GroundedContext = """
            ## Ground Truth:
            1. Recieve the list of applications that need to be updated to the specified TLS version
            2. Request and wait for an approval
            3. Perform the updates one by one, monitoring health for 30 seconds before moving to the next app
            4. App1 should be updated, App2 should be rolled back, the remaining apps should be updated.
            5. Acknowledge that the update is complete.

            ## Expected Response Characteristics
            - The agent should keep the user informed as it performs each step of the update
            - The responses should make good use of emoji, be brief but informative.
            - The response should avoid unnecessary information or ambiguity.
            """;

        evalInput.ExampleResponse = """
            Here are several examples of good responses for a few different steps of the process:

            ## Example 1

            ✅ TLS version update completed for <appname> at 2025-02-24T01:00:00. No anomalies detected.

            ## Example 2

            ▶️ TLS version update initiated for <appname> at 2025-02-24T01:00:00.0000000Z

            ## Example 3

            ⚠️ Traffic anomaly detected for <appname> after TLS update! Attempting rollback to the previous version immediately (TLS 1.0).

            ## Example 4

            🔄 Rollback completed for **App2**. Resuming execution with **App3**.

            ## Example 5

            - ✅ **App1** successfully updated to TLS 1.2 at `2025-02-24T01:00:00Z`. No anomalies detected.
            - ⚠️ **App2** updated to TLS 1.2 at `2025-02-24T01:00:00Z`, but traffic anomalies were detected. 🔄 Rollback to TLS 1.0 completed.
            - ✅ **App3** successfully updated to TLS 1.2 at `2025-02-24T01:00:00Z`. No anomalies detected.
            - ✅ **App4** successfully updated to TLS 1.2 at `2025-02-24T01:00:00Z`. No anomalies detected.
            - ✅ **App5** successfully updated to TLS 1.2 at `2025-02-24T01:00:00Z`. No anomalies detected.

            The update was completed successfully without any issues. 🎉
            """;

        var agentInput = new TlsBestPracticesInput { AppsInViolation = _testApps, DesiredVersion = "1.2", };
        string? instanceID = "";

        try
        {
            var threadId = Guid.NewGuid();
            instanceID = await _agentFactory.StartOrchestration(agentInput, threadId);

            var orchestrationMetadata = await ApprovalTestHelper.WaitForCompletionWithAutomaticApprovals(
                durableTaskClient: _durableTaskClient,
                instanceID,
                threadRepository: _threadRepository,
                threadId,
                logger: null,
                tokenSource.Token);

            var fullHistory = orchestrationMetadata.ReadChatHistory();
            await evalInput.EvaluateAgentResponsesAsync(fullHistory, tokenSource.Token);

            foreach (var app in _testApps)
            {
                TestContext.WriteLine($"Test complete. App {app.Name} is now set to TLS {_mocks.ArmPlugin.GetTlsStatus(app.ResourceId)}");
            }

            Assert.AreEqual("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[0].ResourceId), ignoreCase: true, $"App {_testApps[0].Name} does not have expected TLS setting.");
            Assert.AreEqual("1.0", _mocks.ArmPlugin.GetTlsStatus(_testApps[1].ResourceId), ignoreCase: true, $"App {_testApps[1].Name} does not have expected TLS setting.");
            Assert.AreEqual("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[2].ResourceId), ignoreCase: true, $"App {_testApps[2].Name} does not have expected TLS setting.");
            Assert.AreEqual("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[3].ResourceId), ignoreCase: true, $"App {_testApps[3].Name} does not have expected TLS setting.");
            Assert.AreEqual("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[4].ResourceId), ignoreCase: true, $"App {_testApps[4].Name} does not have expected TLS setting.");
        }
        catch (Grpc.Core.RpcException ex)
        {
            Assert.Fail($"Make sure you have the DTS emulator running (run-durable-emulator.ps1) or your appsettings.development.json has a valid Durable Task Scheduler connection string.{Environment.NewLine} {ex}");
        }
        finally
        {
            if (!string.IsNullOrEmpty(instanceID))
            {
                await _durableTaskClient.TerminateInstanceAsync(instanceID, new TerminateInstanceOptions { Output = "test cleanup", Recursive = true });
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task Tls_AbortOnUnhealthy(string iteration)
    {
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMinutes(5));

        EvalInput evalInput = new EvalInput(_chatConfiguration, this.TestContext, _llmDeploymentName ?? string.Empty);
        evalInput.GroundedContext = """
            ## Ground Truth:
            1. Recieve the list of applications that need to be updated to the specified TLS version
            2. Request and wait for an approval
            3. Perform the updates one by one, monitoring health for 30 seconds before moving to the next app
            4. App1 should be updated, App2 should be rolled back, the remaining apps should not be updated.
            5. Acknowledge that the update is complete.

            ## Expected Response Characteristics
            - The agent should keep the user informed as it performs each step of the update
            - The responses should make good use of emoji, be brief but informative.
            - The response should avoid unnecessary information or ambiguity.
            """;

        evalInput.ExampleResponse = """
            Here are several examples of good responses for a few different steps of the process:

            ## Example 1

            ✅ TLS version update completed for <appname> at 2025-02-24T01:00:00. No anomalies detected.

            ## Example 2

            ▶️ TLS version update initiated for <appname> at 2025-02-24T01:00:00.0000000Z

            ## Example 3

            ⚠️ Traffic anomaly detected for <appname> after TLS update! Attempting rollback to the previous version immediately (TLS 1.0).

            ## Example 4

            🔄 Rollback to TLS 1.0 completed for **App2**. 

            ## Example 5

            - **App1**: ✅ TLS version updated to 1.2 successfully with no anomalies detected.
            - **App2**: ⚠️ Anomaly detected post-update. Rolled back to TLS 1.0.
            - **App3**: ⏸ No update made due to App2 rollback.
            - **App4**: ⏸ No update made due to App2 rollback.
            - **App5**: ⏸ No update made due to App2 rollback.

            The update was completed successfully without any issues. 🎉
            """;

        _mocks.MetricsPlugin.UnhealthyResourceIds.Add(_testApps[1].ResourceId);
        var agentInput = new TlsBestPracticesInput { AppsInViolation = _testApps, DesiredVersion = "1.2", };
        string? instanceID = "";

        try
        {
            var threadId = Guid.NewGuid();
            instanceID = await _agentFactory.StartOrchestration(agentInput, threadId);

            await _durableTaskClient.RaiseEventAsync(instanceID, "NewChatMessage", new ChatMessage
            (
                ChatRole.User,
                "If any apps become unhealthy then complete the rollback for the unhealthy app, but then do not proceed with any more updates."
            ));

            var orchestrationMetadata = await ApprovalTestHelper.WaitForCompletionWithAutomaticApprovals(
                durableTaskClient: _durableTaskClient,
                instanceID,
                threadRepository: _threadRepository,
                threadId,
                logger: null,
                tokenSource.Token);

            var fullHistory = orchestrationMetadata.ReadChatHistory();
            await evalInput.EvaluateAgentResponsesAsync(fullHistory, tokenSource.Token);

            foreach (var app in _testApps)
            {
                TestContext.WriteLine($"Test complete. App {app.Name} is now set to TLS {_mocks.ArmPlugin.GetTlsStatus(app.ResourceId)}");
            }

            Assert.AreEqual("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[0].ResourceId), ignoreCase: true, $"App {_testApps[0].Name} does not have expected TLS setting.");
            Assert.AreEqual("1.0", _mocks.ArmPlugin.GetTlsStatus(_testApps[1].ResourceId), ignoreCase: true, $"App {_testApps[1].Name} does not have expected TLS setting.");
            Assert.AreEqual("1.0", _mocks.ArmPlugin.GetTlsStatus(_testApps[2].ResourceId), ignoreCase: true, $"App {_testApps[2].Name} does not have expected TLS setting.");
            Assert.AreEqual("1.0", _mocks.ArmPlugin.GetTlsStatus(_testApps[3].ResourceId), ignoreCase: true, $"App {_testApps[3].Name} does not have expected TLS setting.");
            Assert.AreEqual("1.0", _mocks.ArmPlugin.GetTlsStatus(_testApps[4].ResourceId), ignoreCase: true, $"App {_testApps[4].Name} does not have expected TLS setting.");
        }
        catch (Grpc.Core.RpcException ex)
        {
            Assert.Fail($"Make sure you have the DTS emulator running (run-durable-emulator.ps1) or your appsettings.development.json has a valid Durable Task Scheduler connection string.{Environment.NewLine} {ex}");
        }
        finally
        {
            if (!string.IsNullOrEmpty(instanceID))
            {
                await _durableTaskClient.TerminateInstanceAsync(instanceID, new TerminateInstanceOptions { Output = "test cleanup", Recursive = true });
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task Tls_AskForConfirmationOnRollback(string iteration)
    {
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMinutes(5));

        EvalInput evalInput = new EvalInput(_chatConfiguration, this.TestContext, _llmDeploymentName ?? string.Empty);
        evalInput.GroundedContext = """
            ## Ground Truth:
            1. Recieve the list of applications that need to be updated to the specified TLS version
            2. Request and wait for an approval
            3. Perform the updates one by one, monitoring health for 30 seconds before moving to the next app
            4. All five apps should be updated, but when app2 becomes unhealthy, the agent asks the user for input and then follows their instructions.
            5. Acknowledge that the update is complete.

            ## Expected Response Characteristics
            - The agent should keep the user informed as it performs each step of the update
            - The responses should make good use of emoji, be brief but informative.
            - The response should avoid unnecessary information or ambiguity.
            """;

        evalInput.ExampleResponse = """
            Here are several examples of good responses for a few different steps of the process:

            ## Example 1

            ✅ TLS version update completed for <appname> at 2025-02-24T01:00:00. No anomalies detected.

            ## Example 2

            ▶️ TLS version update initiated for <appname> at 2025-02-24T01:00:00.0000000Z

            ## Example 3

            ⚠️ Traffic anomaly detected for <appname> after TLS update! Should I perform a rollback to the previous version (TLS 1.0)? Please confirm.

            ## Example 4

            🔄 Rollback to TLS 1.0 completed for **App2**.

            ## Example 5

            - **App1**: ✅ TLS version updated to 1.2 successfully with no anomalies detected.
            - **App2**: ⚠️ Updated to TLS 1.2, anomaly detected post-update. But user indicated that no rollback was necessary.
            - **App3**: ✅ TLS version updated to 1.2 successfully with no anomalies detected.
            - **App4**: ✅ TLS version updated to 1.2 successfully with no anomalies detected.
            - **App5**: ✅ TLS version updated to 1.2 successfully with no anomalies detected.

            The update was completed successfully. 🎉
            """;

        _mocks.MetricsPlugin.UnhealthyResourceIds.Add(_testApps[1].ResourceId);
        var agentInput = new TlsBestPracticesInput { AppsInViolation = _testApps, DesiredVersion = "1.2", };
        string? instanceID = "";

        try
        {
            var threadId = Guid.NewGuid();
            instanceID = await _agentFactory.StartOrchestration(agentInput, threadId);

            await _durableTaskClient.RaiseEventAsync(instanceID, "NewChatMessage", new ChatMessage
            (
                ChatRole.User,
                "If any apps become unhealthy, I want you to ask me for confirmation on whether I want to proceed with the rollback, or leave the app as is. Specifically use the word confirmation when you request it."
            ));

            bool shouldCheckForRollbackMessage = true;
            var orchestrationMetadata = await ApprovalTestHelper.WaitForCompletionWithAutomaticApprovals(
                durableTaskClient: _durableTaskClient,
                instanceID,
                threadRepository: _threadRepository,
                threadId,
                logger: null,
                tokenSource.Token,
                customAction: async () =>
                {
                    var last = _mocks.CommunicationService.Messages.LastOrDefault();

                    // Wait for the model to ask us whether it should perform a rollback.
                    if (shouldCheckForRollbackMessage
                        && last != null
                        && last.Contains("back", StringComparison.InvariantCultureIgnoreCase)
                        && last.Contains("confirm", StringComparison.InvariantCultureIgnoreCase)
                        && last.Contains("?"))
                    {
                        // simulate the user taking a while to respond.
                        await Task.Delay(TimeSpan.FromSeconds(5));

                        await _durableTaskClient.RaiseEventAsync(instanceID, "NewChatMessage", new ChatMessage
                        (
                            ChatRole.User,
                            "I checked the app myself, a rollback is not necessary. You can leave the app as is and proceed."
                        ));
                        shouldCheckForRollbackMessage = false;
                    }
                });

            var fullHistory = orchestrationMetadata.ReadChatHistory();
            await evalInput.EvaluateAgentResponsesAsync(fullHistory, tokenSource.Token);

            foreach (var app in _testApps)
            {
                TestContext.WriteLine($"Test complete. App {app.Name} is now set to TLS {_mocks.ArmPlugin.GetTlsStatus(app.ResourceId)}");
            }

            Assert.AreEqual("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[0].ResourceId), ignoreCase: true, $"App {_testApps[0].Name} does not have expected TLS setting.");
            Assert.AreEqual("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[1].ResourceId), ignoreCase: true, $"App {_testApps[1].Name} does not have expected TLS setting.");
            Assert.AreEqual("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[2].ResourceId), ignoreCase: true, $"App {_testApps[2].Name} does not have expected TLS setting.");
            Assert.AreEqual("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[3].ResourceId), ignoreCase: true, $"App {_testApps[3].Name} does not have expected TLS setting.");
            Assert.AreEqual("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[4].ResourceId), ignoreCase: true, $"App {_testApps[4].Name} does not have expected TLS setting.");
        }
        catch (Grpc.Core.RpcException ex)
        {
            Assert.Fail($"Make sure you have the DTS emulator running (run-durable-emulator.ps1) or your appsettings.development.json has a valid Durable Task Scheduler connection string.{Environment.NewLine} {ex}");
        }
        finally
        {
            if (!string.IsNullOrEmpty(instanceID))
            {
                await _durableTaskClient.TerminateInstanceAsync(instanceID, new TerminateInstanceOptions { Output = "test cleanup", Recursive = true });
            }
        }
    }
}
