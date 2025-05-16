using Agent.Tests.Common.Mocks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agent.Evals;

[TestClass]
public class MetaAgentFunctionsGraphEvals
{
    public TestContext TestContext { get; set; }
    private ChatConfiguration? _chatConfiguration;
    private IHost? _host;
    private MetaAgentMockSetup _mocks;

    private static int _iterationCount = 5;
    private string? _llmDeploymentName;

    static MetaAgentFunctionsGraphEvals()
    {
        _iterationCount = TestHelpers.GetIterationCount(defaultValue: _iterationCount);
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        var builder = TestHelpers.BuildTestApp(out _llmDeploymentName);
        builder.RegisterDefaultServices();

        _mocks = new MetaAgentMockSetup(graphName: "gfuncbad");
        builder.Services.AddMockServices(_mocks);

        _host = builder.Build();
        _mocks.FinishSetup(_host.Services);

        var evalClient = _host.Services.GetRequiredService<IChatClient>();
        IEvaluationTokenCounter? tokenCounter = null;
        _chatConfiguration = new ChatConfiguration(evalClient, tokenCounter);

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
    public async Task MetaAgent_FunctionsGraph_FindAppFullName(string testRunGuid)
    {
        var userMsg = "find my function app pbatum-sre-flexbad-swc-net-http-working. what region is it in?";

        string groundedContext = """
            ## Ground Truth:
            1. The full name of the function app that the user is referring to is `pbatum-sre-flexbad-swc-net-http-working`
            2. The app is in the `Sweden Central` region
            3. The terms region and location are used interchangeably
            
            ## Expected Response Characteristics
            - The response must include the full name of the function app and its location
            """;

        var exampleResponse = $"""
            I found the app `pbatum-sre-flexbad-swc-net-http-working` which is located in the `Sweden Central` region:

            Function App Name: pbatum-sre-flexbad-swc-net-http-working
            Resource Group: pbatum-sre-flexbad-swc-net-http-working
            Subscription ID: 29e3378b-0aaf-45da-b3c6-6fd0eea164e4
            Location: Sweden Central
            Resource ID: /subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-net-http-working/providers/microsoft.web/sites/pbatum-sre-flexbad-swc-net-http-working

            Let me know if you were looking for something else or if you need assistance with this app!
            """;

        var (response, fullConversation) = await PromptModel(testRunGuid, userMsg);
        var evalResult = await response.EvaluateAsync(TestContext, _chatConfiguration, fullConversation, groundedContext, exampleResponse, _llmDeploymentName);
        TestContext.WriteLine($"Agent responds: {response.Text}");

        Assert.IsTrue(evalResult.Equivalence.Value >= 4, $"Low equivalence score: {evalResult.Equivalence.Value}, {evalResult.Equivalence.Reason}");
        Assert.IsTrue(evalResult.Groundedness.Value >= 4, $"Low groundedness score: {evalResult.Groundedness.Value}, {evalResult.Groundedness.Reason}");
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task MetaAgent_FunctionsGraph_FindAppPartialName(string testRunGuid)
    {
        var userMsg = "find my net-http-working function app. what region is it in?";

        string groundedContext = """
            ## Ground Truth:
            1. The full name of the function app that the user is referring to is `pbatum-sre-flexbad-swc-net-http-working`
            2. The app is in the `Sweden Central` region
            3. The terms region and location are used interchangeably
            
            ## Expected Response Characteristics
            - The response must include the full name of the function app and its location
            """;

        var exampleResponse = $"""
            I found the app `pbatum-sre-flexbad-swc-net-http-working` which is located in the `Sweden Central` region:

            Function App Name: pbatum-sre-flexbad-swc-net-http-working
            Resource Group: pbatum-sre-flexbad-swc-net-http-working
            Subscription ID: 29e3378b-0aaf-45da-b3c6-6fd0eea164e4
            Location: Sweden Central
            Resource ID: /subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-net-http-working/providers/microsoft.web/sites/pbatum-sre-flexbad-swc-net-http-working

            Let me know if you were looking for something else or if you need assistance with this app!
            """;

        var (response, fullConversation) = await PromptModel(testRunGuid, userMsg);
        var evalResult = await response.EvaluateAsync(TestContext, _chatConfiguration, fullConversation, groundedContext, exampleResponse, _llmDeploymentName);
        TestContext.WriteLine($"{Environment.NewLine}Agent responds: {response.Text}");

        Assert.IsTrue(evalResult.Equivalence.Value >= 4, $"Low equivalence score: {evalResult.Equivalence.Value}, {evalResult.Equivalence.Reason}");
        Assert.IsTrue(evalResult.Groundedness.Value >= 4, $"Low groundedness score: {evalResult.Groundedness.Value}, {evalResult.Groundedness.Reason}");
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task MetaAgent_FunctionsGraph_ListApps(string testRunGuid)
    {
        var userMsg = "list my function apps";

        string groundedContext = """
            ## Ground Truth:
            1. The user has 14 function apps. Their names are:
                - pbatum-sre-flexbad-swc-net-noclientid
                - pbatum-sre-flexbad-swc-net-http-novnet
                - pbatum-sre-flexbad-swc-net-oom
                - pbatum-sre-flexbad-swc-net-http-nostoragelocalauth
                - pbatum-sre-flexbad-swc-net-queue-working
                - pbatum-sre-flexbad-swc-net-queue-noprivateendpoint
                - pbatum-sre-flexbad-swc-net-http-working
                - pbatum-sre-flexbad-swc-net-timer-working
                - pbatum-sre-flexbad-swc-net-timeroverdue
                - pbatum-sre-flexbad-swc-net-timeoutexceeded
                - pbatum-sre-flexbad-swc-net-http-nostoragerole
                - pbatum-sre-flexbad-swc-py-badasync
                - pbatum-sre-flexbad-swc-net-no-sbconnstring
                - pbatum-sre-flexbad-swc-net-http-throwonstartup

            ## Expected Response Characteristics
            - The response must include all of the listed app names.
            """;

        var exampleResponse = $"""
            Here is the list of your **Function Apps**:

            1. **pbatum-sre-flexbad-swc-net-noclientid**
               - **Resource ID**: `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-net-noclientid/providers/microsoft.web/sites/pbatum-sre-flexbad-swc-net-noclientid`
               - **Location**: Sweden Central

            2. **pbatum-sre-flexbad-swc-net-http-novnet**
               - **Resource ID**: `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-net-http-novnet/providers/microsoft.web/sites/pbatum-sre-flexbad-swc-net-http-novnet`
               - **Location**: Sweden Central

            3. **pbatum-sre-flexbad-swc-net-oom**
               - **Resource ID**: `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-net-oom/providers/microsoft.web/sites/pbatum-sre-flexbad-swc-net-oom`
               - **Location**: Sweden Central

            4. **pbatum-sre-flexbad-swc-net-http-nostoragelocalauth**
               - **Resource ID**: `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-net-http-nostoragelocalauth/providers/microsoft.web/sites/pbatum-sre-flexbad-swc-net-http-nostoragelocalauth`
               - **Location**: Sweden Central

            5. **pbatum-sre-flexbad-swc-net-queue-working**
               - **Resource ID**: `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-net-queue-working/providers/microsoft.web/sites/pbatum-sre-flexbad-swc-net-queue-working`
               - **Location**: Sweden Central

            6. **pbatum-sre-flexbad-swc-net-queue-noprivateendpoint**
               - **Resource ID**: `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-net-queue-noprivateendpoint/providers/microsoft.web/sites/pbatum-sre-flexbad-swc-net-queue-noprivateendpoint`
               - **Location**: Sweden Central

            7. **pbatum-sre-flexbad-swc-net-http-working**
               - **Resource ID**: `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-net-http-working/providers/microsoft.web/sites/pbatum-sre-flexbad-swc-net-http-working`
               - **Location**: Sweden Central

            8. **pbatum-sre-flexbad-swc-net-timer-working**
               - **Resource ID**: `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-net-timer-working/providers/microsoft.web/sites/pbatum-sre-flexbad-swc-net-timer-working`
               - **Location**: Sweden Central

            9. **pbatum-sre-flexbad-swc-net-timeroverdue**
               - **Resource ID**: `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-net-timeroverdue/providers/microsoft.web/sites/pbatum-sre-flexbad-swc-net-timeroverdue`
               - **Location**: Sweden Central

            10. **pbatum-sre-flexbad-swc-net-timeoutexceeded**
                - **Resource ID**: `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-net-timeoutexceeded/providers/microsoft.web/sites/pbatum-sre-flexbad-swc-net-timeoutexceeded`
                - **Location**: Sweden Central

            11. **pbatum-sre-flexbad-swc-net-http-nostoragerole**
                - **Resource ID**: `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-net-http-nostoragerole/providers/microsoft.web/sites/pbatum-sre-flexbad-swc-net-http-nostoragerole`
                - **Location**: Sweden Central

            12. **pbatum-sre-flexbad-swc-py-badasync**
                - **Resource ID**: `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-py-badasync/providers/microsoft.web/sites/pbatum-sre-flexbad-swc-py-badasync`
                - **Location**: Sweden Central

            13. **pbatum-sre-flexbad-swc-net-no-sbconnstring**
                - **Resource ID**: `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-net-no-sbconnstring/providers/microsoft.web/sites/pbatum-sre-flexbad-swc-net-no-sbconnstring`
                - **Location**: Sweden Central

            14. **pbatum-sre-flexbad-swc-net-http-throwonstartup**
                - **Resource ID**: `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-net-http-throwonstartup/providers/microsoft.web/sites/pbatum-sre-flexbad-swc-net-http-throwonstartup`
                - **Location**: Sweden Central

            Let me know if you'd like further information or assistance with any of these apps!
            """;

        var (response, fullConversation) = await PromptModel(testRunGuid, userMsg);
        var evalResult = await response.EvaluateAsync(TestContext, _chatConfiguration, fullConversation, groundedContext, exampleResponse, _llmDeploymentName);
        TestContext.WriteLine($"{Environment.NewLine}Agent responds: {response.Text}");
        Assert.IsTrue(evalResult.Equivalence.Value >= 4, $"Low equivalence score: {evalResult.Equivalence.Value}, {evalResult.Equivalence.Reason}");
        Assert.IsTrue(evalResult.Groundedness.Value >= 4, $"Low groundedness score: {evalResult.Groundedness.Value}, {evalResult.Groundedness.Reason}");
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task MetaAgent_FunctionsGraph_ListAppsByZoneRedundancy(string testRunGuid)
    {
        var userMsg = "which of my function apps have zone redundancy configured and which do not?";

        string groundedContext = """
            ## Ground Truth:
            1. The user has 14 function apps. Their names are:
                - pbatum-sre-flexbad-swc-net-noclientid
                - pbatum-sre-flexbad-swc-net-http-novnet
                - pbatum-sre-flexbad-swc-net-oom
                - pbatum-sre-flexbad-swc-net-http-nostoragelocalauth
                - pbatum-sre-flexbad-swc-net-queue-working
                - pbatum-sre-flexbad-swc-net-queue-noprivateendpoint
                - pbatum-sre-flexbad-swc-net-http-working
                - pbatum-sre-flexbad-swc-net-timer-working
                - pbatum-sre-flexbad-swc-net-timeroverdue
                - pbatum-sre-flexbad-swc-net-timeoutexceeded
                - pbatum-sre-flexbad-swc-net-http-nostoragerole
                - pbatum-sre-flexbad-swc-py-badasync
                - pbatum-sre-flexbad-swc-net-no-sbconnstring
                - pbatum-sre-flexbad-swc-net-http-throwonstartup
            2. None of these apps have zone redundancy configured.

            ## Expected Response Characteristics
            - The response must include all of the listed app names and indicate that none of them have zone redundancy configured.
            """;

        var exampleResponse = $"""
            None of your function apps have zone redundancy configured. Here are the details:

            Zone redundancy enabled:
                **NONE**

            No zone redundancy:
                - pbatum-sre-flexbad-swc-net-noclientid
                - pbatum-sre-flexbad-swc-net-http-novnet
                - pbatum-sre-flexbad-swc-net-oom
                - pbatum-sre-flexbad-swc-net-http-nostoragelocalauth
                - pbatum-sre-flexbad-swc-net-queue-working
                - pbatum-sre-flexbad-swc-net-queue-noprivateendpoint
                - pbatum-sre-flexbad-swc-net-http-working
                - pbatum-sre-flexbad-swc-net-timer-working
                - pbatum-sre-flexbad-swc-net-timeroverdue
                - pbatum-sre-flexbad-swc-net-timeoutexceeded
                - pbatum-sre-flexbad-swc-net-http-nostoragerole
                - pbatum-sre-flexbad-swc-py-badasync
                - pbatum-sre-flexbad-swc-net-no-sbconnstring
                - pbatum-sre-flexbad-swc-net-http-throwonstartup

            Let me know if you'd like further information or assistance with any of these apps!
            """;

        var (response, fullConversation) = await PromptModel(testRunGuid, userMsg);
        var evalResult = await response.EvaluateAsync(TestContext, _chatConfiguration, fullConversation, groundedContext, exampleResponse, _llmDeploymentName);
        TestContext.WriteLine($"{Environment.NewLine}Agent responds: {response.Text}");
        Assert.IsTrue(evalResult.Equivalence.Value >= 4, $"Low equivalence score: {evalResult.Equivalence.Value}, {evalResult.Equivalence.Reason}");
        Assert.IsTrue(evalResult.Groundedness.Value >= 4, $"Low groundedness score: {evalResult.Groundedness.Value}, {evalResult.Groundedness.Reason}");
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task MetaAgent_FunctionsGraph_FindVnet(string testRunGuid)
    {
        var userMsg = "can you find the vnet used by function app pbatum-sre-flexbad-swc-net-http-working?";

        string groundedContext = """
            ## Ground Truth:
            1. The full name of the function app that the user is referring to is `pbatum-sre-flexbad-swc-net-http-working`
            2. The app is connected to a subnet named `app` with resource ID `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/pbatum-sre-flexbad-swc-net-http-working/providers/Microsoft.Network/virtualNetworks/vnet-jrfup2e7rs4pg/subnets/app`
            3. The subnet is part of a VNet named `vnet-jrfup2e7rs4pg`
            
            ## Expected Response Characteristics
            - The response must include the full name of the function app and the name of the VNet.
            - The response should include mention of the subnet.
            """;

        var exampleResponse = $"""
            The app **pbatum-sre-flexbad-swc-net-http-working** is connected to the following Virtual Network (VNet):

            VNet Name: vnet-jrfup2e7rs4pg  
            Subnet: app  
            Resource ID: `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/pbatum-sre-flexbad-swc-net-http-working/providers/Microsoft.Network/virtualNetworks/vnet-jrfup2e7rs4pg/subnets/app`

            Let me know if you need further assistance!
            """;

        var (response, fullConversation) = await PromptModel(testRunGuid, userMsg);
        var evalResult = await response.EvaluateAsync(TestContext, _chatConfiguration, fullConversation, groundedContext, exampleResponse, _llmDeploymentName);
        TestContext.WriteLine($"{Environment.NewLine}Agent responds: {response.Text}");
        Assert.IsTrue(evalResult.Equivalence.Value >= 4, $"Low equivalence score: {evalResult.Equivalence.Value}, {evalResult.Equivalence.Reason}");
        Assert.IsTrue(evalResult.Groundedness.Value >= 4, $"Low groundedness score: {evalResult.Groundedness.Value}, {evalResult.Groundedness.Reason}");
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task MetaAgent_FunctionsGraph_FindStorageAccount(string testRunGuid)
    {
        var userMsg = "can you find the storage account used by function app pbatum-sre-flexbad-swc-net-http-working?";

        string groundedContext = """
            ## Ground Truth:
            1. The full name of the function app that the user is referring to is `pbatum-sre-flexbad-swc-net-http-working`
            2. The app is using the storage account stjrfup2e7rs4pg with resource ID `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/pbatum-sre-flexbad-swc-net-http-working/providers/Microsoft.Storage/storageAccounts/stjrfup2e7rs4pg`
            3. This storage account is being used in two different ways:
                - For AzureWebJobsStorage, via the AzureWebJobsStorage__accountName appsetting
                - For application package deployment
            
            ## Expected Response Characteristics
            - The response must include the full name of the function app and the name of the storage account.
            """;

        var exampleResponse = $"""
            The app **pbatum-sre-flexbad-swc-net-http-working** is using the following storage account:

            Storage Account Name: stjrfup2e7rs4pg
            Resource ID: `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/pbatum-sre-flexbad-swc-net-http-working/providers/Microsoft.Storage/storageAccounts/stjrfup2e7rs4pg`

            Let me know if you need further assistance!
            """;

        var (response, fullConversation) = await PromptModel(testRunGuid, userMsg);
        var evalResult = await response.EvaluateAsync(TestContext, _chatConfiguration, fullConversation, groundedContext, exampleResponse, _llmDeploymentName);
        TestContext.WriteLine($"{Environment.NewLine}Agent responds: {response.Text}");
        Assert.IsTrue(evalResult.Equivalence.Value >= 4, $"Low equivalence score: {evalResult.Equivalence.Value}, {evalResult.Equivalence.Reason}");
        Assert.IsTrue(evalResult.Groundedness.Value >= 4, $"Low groundedness score: {evalResult.Groundedness.Value}, {evalResult.Groundedness.Reason}");
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task MetaAgent_FunctionsGraph_FindIdentityConfiguration(string testRunGuid)
    {
        var userMsg = "what is the identity configuration for function app pbatum-sre-flexbad-swc-net-http-working?";

        string groundedContext = """
            ## Ground Truth:
            1. The full name of the function app that the user is referring to is `pbatum-sre-flexbad-swc-net-http-working`
            2. The app has a user assigned identity with name `id-processor-jrfup2e7rs4pg` and resource ID `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-net-http-working/providers/microsoft.managedidentity/userassignedidentities/id-processor-jrfup2e7rs4pg`
            
            ## Expected Response Characteristics
            - The response must include the full name of the function app and indicate that it has a user assigned identity and include its name.
            """;

        var exampleResponse = $"""
            The app **pbatum-sre-flexbad-swc-net-http-working** has a user assigned identity with the following details:

            Identity Name: id-processor-jrfup2e7rs4pg
            Resource ID: `/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-flexbad-swc-net-http-working/providers/microsoft.managedidentity/userassignedidentities/id-processor-jrfup2e7rs4pg`

            Let me know if you need further assistance!
            """;

        var (response, fullConversation) = await PromptModel(testRunGuid, userMsg);
        var evalResult = await response.EvaluateAsync(TestContext, _chatConfiguration, fullConversation, groundedContext, exampleResponse, _llmDeploymentName);
        TestContext.WriteLine($"{Environment.NewLine}Agent responds: {response.Text}");
        Assert.IsTrue(evalResult.Equivalence.Value >= 4, $"Low equivalence score: {evalResult.Equivalence.Value}, {evalResult.Equivalence.Reason}");
        Assert.IsTrue(evalResult.Groundedness.Value >= 4, $"Low groundedness score: {evalResult.Groundedness.Value}, {evalResult.Groundedness.Reason}");
    }
}
