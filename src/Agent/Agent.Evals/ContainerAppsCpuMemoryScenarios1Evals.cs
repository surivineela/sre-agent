using System.Diagnostics;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Services;
using Agent.Tests.Common;
using Agent.Tests.Common.Mocks;
using Agent.Tests.Common.Mocks.FunctionCalling;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agent.Evals;

[DoNotParallelize]
[TestClass]
public class ContainerAppsCpuMemoryScenarios1Evals
{
    public TestContext TestContext { get; set; }
    private ChatConfiguration? _chatConfiguration;
    private IChatClient _agentStateAssessmentClient;
    private IHost? _host;

    private static int _iterationCount = 1;
    private string? _llmDeploymentName;

    private ThreadManagementService _threadManager;
    private IThreadRepository _threadRepo;

    private ReplayToolFactory<AgentContext> _replayToolFactory;

    static ContainerAppsCpuMemoryScenarios1Evals()
    {
        _iterationCount = TestHelpers.GetIterationCount(defaultValue: _iterationCount);
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        var builder = TestHelpers.BuildTestApp(out _llmDeploymentName);
        builder.RegisterDefaultServices();
        builder.Services.AddLocalGremlin("gmrsharmacadiag");
        builder.RegisterServicesForAgentFrameworkEval();

        // required because InboundCommunicationService has code for handling durable
        builder.ConfigureDurable();

        // register the definitions for the plugins that the agent will use, but we don't need plugin implementations because we are using tool replay.
        builder.Services.AddSingleton(sp => new ContainerAppPluginDefinition(null));
        builder.Services.AddSingleton(sp => new NSGRulePluginDefinition(null));
        builder.Services.AddSingleton(sp => new ArmPluginDefinition(null));

        _host = builder.Build();

        _threadManager = _host.Services.GetRequiredService<ThreadManagementService>();
        _threadRepo = _host.Services.GetRequiredService<IThreadRepository>();
        _replayToolFactory = (ReplayToolFactory<AgentContext>) _host.Services.GetRequiredService<IToolFactory<AgentContext>>();

        var evalClient = _host.Services.GetRequiredService<IChatClient>();
        _chatConfiguration = new ChatConfiguration(evalClient, tokenCounter: null);
        _agentStateAssessmentClient = _host.Services.GetRequiredKeyedService<IChatClient>("function-invocation-enabled");

        await _host.StartAsync();
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    public static IEnumerable<object[]> InvestigateMemoryLeakApp()
    {
        yield return new object[] { Guid.NewGuid().ToString(),
            "My container app is experiencing a bunch of 500 errors, can you diagnose: /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourceGroups/mrsharm-operations-agent-3p-rg/providers/Microsoft.App/containerApps/diagnosticbench-app-202504091010" };

        yield return new object[] { Guid.NewGuid().ToString(),
            "What's wrong with my container app? I see responses with status code 500. /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourceGroups/mrsharm-operations-agent-3p-rg/providers/Microsoft.App/containerApps/diagnosticbench-app-202504091010" };

        yield return new object[] { Guid.NewGuid().ToString(),
            "Help me diagnose why my container app is returning 500 errors: /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourceGroups/mrsharm-operations-agent-3p-rg/providers/Microsoft.App/containerApps/diagnosticbench-app-202504091010" };

        yield return new object[] { Guid.NewGuid().ToString(),
            "I'm getting HTTP 500 responses from my container app. Can you investigate? /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourceGroups/mrsharm-operations-agent-3p-rg/providers/Microsoft.App/containerApps/diagnosticbench-app-202504091010" };

        yield return new object[] { Guid.NewGuid().ToString(),
            "My container app keeps failing with 500 status codes. Need help troubleshooting: /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourceGroups/mrsharm-operations-agent-3p-rg/providers/Microsoft.App/containerApps/diagnosticbench-app-202504091010" };
    }

    [TestMethod]
    [DynamicData(nameof(InvestigateMemoryLeakApp), DynamicDataSourceType.Method)]
    public async Task ContainerAppsCpuMemoryScenarios1_DiagnoseMemoryLeak(string testRunGuid, string userMsg)
    {
        var tokenSource = new CancellationTokenSource();
        if (!Debugger.IsAttached)
        {
            tokenSource.CancelAfter(TimeSpan.FromMinutes(2));
        }

        foreach (var f in Directory.GetFiles(Path.Combine("ToolReplayLogs", "ContainerAppsMemoryEvals")))
        {
            _replayToolFactory.LoadLogFromString(File.ReadAllText(f));
        }

        string groundedContext = """
            ## Ground Truth:
            1. The user asks the agent to assist with diagnosis of 500 errors in their container app /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourceGroups/mrsharm-operations-agent-3p-rg/providers/Microsoft.App/containerApps/diagnosticbench-app-202504091010
            2. The application has critical memory issues, details include:
               - The app is experiencing persistent System.OutOfMemoryException errors causing the 500 errors
               - Memory analysis reveals the heap is dominated by System.Byte[] objects (≈2.9 GB, 909 objects)
               - The memory leak has a clear GC root chain starting from AsyncTaskMethodBuilder through WebApplication, ServiceProvider, and eventually to large byte arrays
               - The issue is specifically related to a memory leak in the main business logic around Program.cs:526
               - The largest GC roots point to System.Byte[] arrays with over 5GB of memory consumption
            3. Immediate remediation involves restarting the container app to temporarily restore service
            4. Long-term fix requires engineering to address the underlying memory leak in the code
        """;

        var exampleResponse = $"""
            ⚠️ Incident Summary for diagnosticbench-app-202504091010

            Findings
            Critical Issue: The app is experiencing persistent System.OutOfMemoryException errors, causing 500 errors and service degradation.
            Memory Analysis:
            The heap is dominated by System.Byte[] objects (≈2.9 GB, 909 objects).
            The largest GC root chain is:
            System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Threading.Tasks.VoidTaskResult>+AsyncStateMachineBox<Microsoft.Extensions.Hosting.HostingAbstractionsHostExtensions+<RunAsync>d__4>
              → Microsoft.AspNetCore.Builder.WebApplication
              → Microsoft.Extensions.DependencyInjection.ServiceProvider
              → ... (multiple DI and configuration objects)
              → Program+<>c__DisplayClass0_0
              → System.Collections.Generic.List<System.Byte[]>
              → System.Byte[][]
              → System.Byte[]
            This pattern strongly indicates a memory leak in the main business logic, especially around
            Program.cs:526
            .
            Memory GC Roots (Top 3)
            GC Root Name  Size (Bytes)
            System.Runtime.CompilerServices.AsyncTaskMethodBuilder... → System.Byte[]  5,033,165,088
            Microsoft.AspNetCore.Builder.WebApplication → ... → System.Byte[]  5,033,165,088
            System.Object[] → ... → System.Byte[]  3,774,873,816
            Bar Chart: GC Roots by Size

            System.Runtime.CompilerServices.AsyncTaskMethodBuilder|5033165088
            Microsoft.AspNetCore.Builder.WebApplication|5033165088
            System.Object[]|3774873816

            If you need a full memory dump, further analysis or assistance with addressing the issue, let me know.
            """;

        TestContext.WriteLine(userMsg);
        var startMessageRequest = new CreateMessageRequest(userMsg, "testUser", "Test User");
        var thread = await _threadManager.CreateUserInitiatedThread(new CreateThreadRequest(startMessageRequest));

        List<ChatMessage>? fullConversation = null;
        var autoReplyHelper = new AutoReplyHelper(_agentStateAssessmentClient)
        {
            DefaultReply = "Please do your best to figure it out.",
            GroundedContext = groundedContext
        };

        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));

                (_, fullConversation) = await _threadRepo.WaitForAgentResponse(thread, tokenSource.Token);
                _replayToolFactory.CheckForReplayFailures();

                var reply = await autoReplyHelper.GetReply(fullConversation);

                if (reply != null)
                {
                    await _threadManager.CreateMessage(thread.Id, new CreateMessageRequest(reply, "testUser", "Test User"));
                }

                if (autoReplyHelper.AssessedState == AutoReplyHelper.AssessedAgentState.Findings)
                {
                    break;
                }
            }

            var combinedAgentResponse = fullConversation.CombineAgentResponses();
            var evalResult = await combinedAgentResponse.EvaluateAsync(TestContext, _chatConfiguration, fullConversation, groundedContext, exampleResponse, _llmDeploymentName);

            TestContext.WriteLine(string.Empty);
            TestContext.WriteMessages(fullConversation);

            Assert.IsTrue(evalResult.Equivalence.Value >= 4, $"Low equivalence score: {evalResult.Equivalence.Value}, {evalResult.Equivalence.Reason}");
            Assert.IsTrue(evalResult.Groundedness.Value >= 4, $"Low groundedness score: {evalResult.Groundedness.Value}, {evalResult.Groundedness.Reason}");
        }
        catch (ReplayFailureException fe)
        {
            TestContext.WriteLine(string.Empty);
            TestContext.WriteMessages(fullConversation);
            Assert.Inconclusive($"The agent made a tool call that we could not replay from the logs, which invalidates this test run: {fe.Message}");
        }
        catch(Exception e)
        {
            TestContext.WriteLine(string.Empty);
            TestContext.WriteMessages(fullConversation);
            throw;
        }
    }
}
