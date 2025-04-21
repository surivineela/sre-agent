using Agent.Core.Interfaces;
using Agent.Plugins;
using Agent.Runtime.SubAgents.KubernetesAgent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.DurableTask.Client;
using Agent.Core.Extensions;
using Microsoft.DurableTask.Client.AzureManaged;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker;
using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask.Worker.AzureManaged;
using Agent.Plugins.Definitions;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Plugins.Implementation;
using Agent.Core.Helpers;
using Microsoft.Extensions.AI.Evaluation;
using Agent.Core.Services;
using Agent.Runtime.SubAgents;
using Moq;
using Agent.Prometheus.Services;
using Agent.Runtime.Communication;
using Agent.Tests.Common;
using Agent.Tests.Common.Mocks;
using Agent.Tests.Common.ScenarioTestHelpers;
using Agent.Plugins.Mocks;

namespace Agent.Evals;

// !! Note:
// To run this test, you need to have a valid AKS cluster installed with these test apps: https://github.com/wonderflow/opentelemetry-demo/tree/sre-demo/kubernetes
// The agent needs to have access to the cluster.
[TestClass]
public sealed class AKSAgentEvals
{
    public TestContext TestContext { get; set; }

    private IHost _host;
    private ChatConfiguration _chatConfiguration;
    private KubernetesAgentFactory _kubernetesAgentFactory;
    private DurableTaskClient _durableTaskClient;
    private BasicMockSetup _mocks;
    private MockKubePlugin _mockKubePlugin;

    private string _llmDeploymentName;
    private static int _iterationCount = 1; // Default value

    private string _subscriptionId = "5b6786e7-4668-46b2-89e6-001c8ac90967";
    private string _resourceGroupName = "mltest";
    private string _aksClusterName = "sre-agent-jianbo";
    private string _deploymentNamespace = "default";

    // Static constructor to initialize _iterationCount
    static AKSAgentEvals()
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
            Console.WriteLine($"AKSAgentEvals Static Constructor: IterationCount not found or invalid. Using default value: {_iterationCount}.");
        }
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        // Create thread repository first

        var builder = TestHelpers.BuildTestApp(out _llmDeploymentName);
        builder.RegisterDefaultServices();
        // We are using dts simulator to satisfy the durable task client below by:
        // docker run --rm -it --name dts-emulator -p 14280:8080 -p 14282:8082 -e ClientAuth__DisableAuthentication=true mcr.microsoft.com/dts/dts-emulator:v0.0.6
        builder.ConfigureDurable();

        _mocks = new BasicMockSetup(DateTimeOffset.Parse("2025-02-24T01:00:00Z"), null);

        var services = builder.Services;
        services.AddMockServices(_mocks);
        AKSTestHelpers.AddPluginDefinitions(services);
        services.AddSingleton<ToolsRepository>();
        services.AddSingleton<KubePluginDefinition>();

        _mockKubePlugin = new MockKubePlugin();
        builder.Services.AddSingleton<IKubePlugin>(_mockKubePlugin);
        builder.Services.AddSingleton<KubernetesAgentFactory>();


        var sp = builder.Services.BuildServiceProvider();

        _durableTaskClient = sp.GetRequiredService<DurableTaskClient>();
        _kubernetesAgentFactory = sp.GetRequiredService<KubernetesAgentFactory>();

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
            yield return new object[] { Guid.NewGuid() };
        }
    }

    public static string FormatAKSResourceId(string subscriptionId, string resourceGroupName, string aksClusterName)
    {
        return $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.ContainerService/managedClusters/{aksClusterName}";
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task AKSAgentGenerateResourceGraph(Guid testRunGuid)
    {
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMinutes(5));
        EvalInput evalInput = new EvalInput(_chatConfiguration, this.TestContext, _llmDeploymentName);
        evalInput.GroundedContext = """
            ## Ground Truth:
            1. Subscription ID, resource group, AKS cluster name, resource namespace and name are provided clearly.
            2. Agent can access to the AKS cluster by generating the resource ID from the information.
            3. Agent can generate the resource graph for the AKS cluster.

            ## Expected Response Characteristics
            - The response should clearly explain dependency relationships starting from the input component.
            - The response listed the component names, types (if not Deployment)
            """;

        evalInput.ExampleResponse = $"""
            Here's the microservices topology relationship for the checkout deployment:
            checkout depends on [cart, currency, email, payment, product-catalog, shipping, kafka]
            cart depends on valkey-cart
            product-catalog depends on redis (StatefulSet)
            shipping depends on quote
            """;

        var deploymentName = "checkout";
        var agentInput = $"""
        Can you draw a dependency graph for the following components in the AKS cluster?
        - Subscription ID: {_subscriptionId}
        - Resource Group: {_resourceGroupName}
        - AKS Cluster Name: {_aksClusterName}
        - Deployment Namespace: {_deploymentNamespace}
        - Deployment Name: {deploymentName}
        """;
        string? instanceID = "";

        _mockKubePlugin.ConfigureNamespaces(
            FormatAKSResourceId(_subscriptionId, _resourceGroupName, _aksClusterName),
            "default, kube-system, kube-public");
        _mockKubePlugin.ConfigureDeployments(
            FormatAKSResourceId(_subscriptionId, _resourceGroupName, _aksClusterName),
            _deploymentNamespace,
            "checkout, cart, currency, email, payment, product-catalog, shipping, kafka, valkey-cart, quote");
        _mockKubePlugin.ConfigureStatefulSets(
            FormatAKSResourceId(_subscriptionId, _resourceGroupName, _aksClusterName),
            _deploymentNamespace,
            "redis");

        _mocks.GraphDBPlugin.ConfigureAKSMicroservices(
            FormatAKSResourceId(_subscriptionId, _resourceGroupName, _aksClusterName),
            _deploymentNamespace,
            deploymentName,
            "checkout depends on [cart, currency, email, payment, product-catalog, shipping, kafka], cart depends on valkey-cart, product-catalog depends on redis (StatefulSet), shipping depends on quote");
        _mocks.GraphDBPlugin.ConfigureAKSMicroservices(
            FormatAKSResourceId(_subscriptionId, _resourceGroupName, _aksClusterName),
            _deploymentNamespace,
            "cart",
            "cart depends on valkey-cart");
        _mocks.GraphDBPlugin.ConfigureAKSMicroservices(
            FormatAKSResourceId(_subscriptionId, _resourceGroupName, _aksClusterName),
            _deploymentNamespace,
            "product-catalog",
            "product-catalog depends on redis (StatefulSet)");
        _mocks.GraphDBPlugin.ConfigureAKSMicroservices(
            FormatAKSResourceId(_subscriptionId, _resourceGroupName, _aksClusterName),
            _deploymentNamespace,
            "shipping",
            "shipping depends on quote");
        try
        {
            instanceID = await _kubernetesAgentFactory.StartOrchestration(agentInput, testRunGuid);

            // Continue with orchestration
            var orchestrationMetadata = await _durableTaskClient.WaitForInstanceCompletionAsync(instanceID, getInputsAndOutputs: true, tokenSource.Token);
            Assert.IsTrue(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed);

            var fullHistory = orchestrationMetadata.ReadChatHistory();
            var results = await evalInput.EvaluateAgentResponsesAsync(fullHistory);
            bool hasHighMatch = false;
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                if (result.Equivalence.Value >= 4)
                {
                    hasHighMatch = true;
                }
            }
            Assert.AreEqual(_mockKubePlugin.AksClusterResourceId, FormatAKSResourceId(_subscriptionId, _resourceGroupName, _aksClusterName), ignoreCase: true, $"AKS cluster resource ID is not as expected.");
            if (!hasHighMatch)
            {
                Assert.Fail("No any high equivalency result match in the chat history, indicates the test failed.");
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

