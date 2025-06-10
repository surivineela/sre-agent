using Agent.Tests.Common.Mocks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agent.Evals;

[TestClass]
public class MetaAgentSimpleWebAppGraphEvals
{
    public TestContext TestContext { get; set; }
    private ChatConfiguration? _chatConfiguration;
    private IHost? _host;
    private MetaAgentMockSetup _mocks;

    private static int _iterationCount = 5;
    private string? _llmDeploymentName;

    static MetaAgentSimpleWebAppGraphEvals()
    {
        _iterationCount = TestHelpers.GetIterationCount(defaultValue: _iterationCount);
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        var builder = TestHelpers.BuildTestApp(out _llmDeploymentName);
        builder.RegisterDefaultServices();

        _mocks = new MetaAgentMockSetup(graphName: "gsimpleweb");
        builder.Services.AddMockServices(_mocks);

        _host = builder.Build();
        _mocks.FinishSetup(_host.Services);

        var evalClient = _host.Services.GetRequiredService<IChatClient>();
        _chatConfiguration = new ChatConfiguration(evalClient);

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

    private static IEnumerable<object[]> TestData_Iterations()
    {
        for (int i = 0; i < _iterationCount; i++)
        {
            yield return new object[] { Guid.NewGuid().ToString() };
        }
    }

    private async Task<(ChatResponse, IEnumerable<ChatMessage>)> PromptModel(string testRunGuid, string userMsg)
    {
        var threadId = Guid.Parse(testRunGuid);
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, "This system prompt will be replaced by the meta agent."),
            new ChatMessage(ChatRole.User, userMsg),
        };

        var agentContext = _mocks.GetDefaultMetaAgentContext(testRunGuid);
        var response = await _mocks.Agent.GetModelResponse(agentContext, threadId, messages);
        return (response, messages.Concat(response.Messages));
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task MetaAgent_SimpleWebAppGraph_ListAppsBySku(string testRunGuid)
    {
        var userMsg = "list my webapps by sku";

        string groundedContext = """
            ## Ground Truth:
            1. The user has four web apps in the `eastasia` region
            2. Apps `pbatum-sre-web-eas1` and `pbatum-sre-web-eas2` are in the `Standard` SKU, size S1
            3. Apps `pbatum-sre-web-eas3` and `pbatum-sre-web-eas4` are in the `Premium0V3` SKU, size P0v3
            
            ## Expected Response Characteristics
            - The response must include the full name of four applications, clearly show their size, and group them by SKU.
            """;

        var exampleResponse = $"""
            Here are your web apps categorized by their SKU:

            ### **Standard SKU:**
            1. **App Name:** pbatum-sre-web-eas1  
               **Location:** eastasia  
               **SKU Size:** S1  
               **Resource Group:** pbatum-sre-web-eas  

            2. **App Name:** pbatum-sre-web-eas2  
               **Location:** eastasia  
               **SKU Size:** S1  
               **Resource Group:** pbatum-sre-web-eas  

            ### **Premium0V3 SKU:**
            1. **App Name:** pbatum-sre-web-eas3  
               **Location:** eastasia  
               **SKU Size:** P0v3  
               **Resource Group:** pbatum-sre-web-eas-lin  

            2. **App Name:** pbatum-sre-web-eas4  
               **Location:** eastasia  
               **SKU Size:** P0v3  
               **Resource Group:** pbatum-sre-web-eas-lin

            Let me know if you'd like further information or assistance with any of these apps!
            """;

        var (response, fullConversation) = await PromptModel(testRunGuid, userMsg);
        var evalResult = await response.EvaluateAsync(TestContext, _chatConfiguration, fullConversation, groundedContext, exampleResponse, _llmDeploymentName);
        TestContext.WriteLine($"Agent responds: {response.Text}");

        Assert.IsTrue(evalResult.Equivalence.Value >= 4, $"Low equivalence score: {evalResult.Equivalence.Value}, {evalResult.Equivalence.Reason}");
        Assert.IsTrue(evalResult.Groundedness.Value >= 4, $"Low groundedness score: {evalResult.Groundedness.Value}, {evalResult.Groundedness.Reason}");
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task MetaAgent_SimpleWebAppGraph_ListLinuxApps(string testRunGuid)
    {
        var userMsg = "list my linux webapps";

        string groundedContext = """
            ## Ground Truth:
            1. The user has two linux webapps, `pbatum-sre-web-eas3` and `pbatum-sre-web-eas4`
            
            ## Expected Response Characteristics
            - The response must include the full name of both applications, and clearly indicate that they are linux web apps.
            """;

        var exampleResponse = $"""
            Here are your Linux Web Apps:

            1. **pbatum-sre-web-eas3** (Resource Group: **pbatum-sre-web-eas-lin**, Subscription ID: **29e3378b-0aaf-45da-b3c6-6fd0eea164e4**)
            2. **pbatum-sre-web-eas4** (Resource Group: **pbatum-sre-web-eas-lin**, Subscription ID: **29e3378b-0aaf-45da-b3c6-6fd0eea164e4**)

            Let me know if you need further details or assistance with any of these web apps!
            """;

        var (response, fullConversation) = await PromptModel(testRunGuid, userMsg);
        var evalResult = await response.EvaluateAsync(TestContext, _chatConfiguration, fullConversation, groundedContext, exampleResponse, _llmDeploymentName);
        TestContext.WriteLine($"Agent responds: {response.Text}");

        Assert.IsTrue(evalResult.Equivalence.Value >= 4, $"Low equivalence score: {evalResult.Equivalence.Value}, {evalResult.Equivalence.Reason}");
        Assert.IsTrue(evalResult.Groundedness.Value >= 4, $"Low groundedness score: {evalResult.Groundedness.Value}, {evalResult.Groundedness.Reason}");
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task MetaAgent_SimpleWebAppGraph_ShareAppServicePlan(string testRunGuid)
    {
        var userMsg = "what web apps share an appservice plan with pbatum-sre-web-eas1?";

        string groundedContext = """
            ## Ground Truth:
            1. Apps `pbatum-sre-web-eas1` and `pbatum-sre-web-eas2` are on the same app service plan named `ASP-pbatumsrewebeas-8754`.
            
            ## Expected Response Characteristics
            - The response must include the full name of both applications and clearly explain that they are on the same app service plan.
            """;

        var exampleResponse = $"""
            There is one other app that shares an App Service plan with pbatum-sre-web-eas1, that app is **pbatum-sre-web-eas2** and the plan is **ASP-pbatumsrewebeas-8754**.
            """;

        var (response, fullConversation) = await PromptModel(testRunGuid, userMsg);
        var evalResult = await response.EvaluateAsync(TestContext, _chatConfiguration, fullConversation, groundedContext, exampleResponse, _llmDeploymentName);
        TestContext.WriteLine($"Agent responds: {response.Text}");

        Assert.IsTrue(evalResult.Equivalence.Value >= 4, $"Low equivalence score: {evalResult.Equivalence.Value}, {evalResult.Equivalence.Reason}");
        Assert.IsTrue(evalResult.Groundedness.Value >= 4, $"Low groundedness score: {evalResult.Groundedness.Value}, {evalResult.Groundedness.Reason}");
    }

    public static IEnumerable<object[]> VMCountUserMsgTestData()
    {
        yield return new object[] { Guid.NewGuid().ToString(), "how many VMs is pbatum-sre-web-eas1 running on?" };
        yield return new object[] { Guid.NewGuid().ToString(), "how many instances does pbatum-sre-web-eas1 have?" };
        yield return new object[] { Guid.NewGuid().ToString(), "what is the VM count for pbatum-sre-web-eas1?" };
        yield return new object[] { Guid.NewGuid().ToString(), "how many servers are running pbatum-sre-web-eas1?" };
        yield return new object[] { Guid.NewGuid().ToString(), "number of machines hosting pbatum-sre-web-eas1?" };
    }

    [TestMethod]
    [DynamicData(nameof(VMCountUserMsgTestData), DynamicDataSourceType.Method)]
    public async Task MetaAgent_SimpleWebAppGraph_VMCount(string testRunGuid, string userMsg)
    {
        string groundedContext = """
            ## Ground Truth:
            1. The app `pbatum-sre-web-eas1` is running on App Service plan `ASP-pbatumsrewebeas-8754` which has 1 instance.
            2. Customers will sometimes use the term "VM" to refer to an instance of an App Service plan.
            
            ## Expected Response Characteristics
            - The response must include the full name of the application and clearly state that its running on 1 instance.
            """;

        var exampleResponse = $"""
            The app pbatum-sre-web-eas1 is on App Service plan ASP-pbatumsrewebeas-8754 which has **1** instance.
            """;

        var (response, fullConversation) = await PromptModel(testRunGuid, userMsg);
        var evalResult = await response.EvaluateAsync(TestContext, _chatConfiguration, fullConversation, groundedContext, exampleResponse, _llmDeploymentName);
        TestContext.WriteLine($"Agent responds: {response.Text}");

        Assert.IsTrue(evalResult.Equivalence.Value >= 4, $"Low equivalence score: {evalResult.Equivalence.Value}, {evalResult.Equivalence.Reason}");
        Assert.IsTrue(evalResult.Groundedness.Value >= 4, $"Low groundedness score: {evalResult.Groundedness.Value}, {evalResult.Groundedness.Reason}");
    }

}
