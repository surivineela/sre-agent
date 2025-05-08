using Agent.Plugins;
using Agent.Runtime.SubAgents.KubernetesAgent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI.Evaluation;
using Agent.Runtime.SubAgents;
using Agent.Tests.Common;
using Agent.Tests.Common.Mocks;
using Agent.Tests.Common.ScenarioTestHelpers;
using Agent.Plugins.Mocks;
using Moq;
using Agent.Core.Interfaces;

namespace Agent.Evals;

[TestClass]
public sealed class AKSAgentEvals
{
    public TestContext TestContext { get; set; }

    private IHost _host;
    private ChatConfiguration _chatConfiguration;
    private KubernetesAgentFactory _kubernetesAgentFactory;
    private DurableTaskClient _durableTaskClient;
    private IThreadRepository _threadRepository;
    private BasicMockSetup _mocks;
    private Mock<IKubePlugin> _mockKubePluginWrapper;
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
        services.AddSingleton<IToolsRepository, ToolsRepository>();
        services.AddSingleton<KubePluginDefinition>();

        _mockKubePlugin = new MockKubePlugin();

        _mockKubePluginWrapper = new Mock<IKubePlugin>();
        builder.Services.AddSingleton<KubernetesAgentFactory>();

        // This setup ensures we can configure the mock easily via _mockKubePlugin
        // and verify calls using _mockKubePluginWrapper.
        _mockKubePluginWrapper.Setup(x => x.GetAKSClusterResourceIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string sub, string rg, string cluster) => _mockKubePlugin.GetAKSClusterResourceIdAsync(sub, rg, cluster));
        _mockKubePluginWrapper.Setup(x => x.GetKubeNamespacesAsync(It.IsAny<string>()))
             .Returns((string id) => _mockKubePlugin.GetKubeNamespacesAsync(id));
        _mockKubePluginWrapper.Setup(x => x.GetKubeDeploymentsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string id, string ns) => _mockKubePlugin.GetKubeDeploymentsAsync(id, ns));
        _mockKubePluginWrapper.Setup(x => x.ListKubeResourcesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string id, string ns, string kind) => _mockKubePlugin.ListKubeResourcesAsync(id, ns, kind));
        _mockKubePluginWrapper.Setup(x => x.GetRecentlyUpdatedWorkloadsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns((string id, string ns, int min) => _mockKubePlugin.GetRecentlyUpdatedWorkloadsAsync(id, ns, min));
        _mockKubePluginWrapper.Setup(x => x.GetKubeResourceEventsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string id, string ns, string g, string k, string n) => _mockKubePlugin.GetKubeResourceEventsAsync(id, ns, g, k, n));
        _mockKubePluginWrapper.Setup(x => x.GetKubePodsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string id, string ns, string k, string n) => _mockKubePlugin.GetKubePodsAsync(id, ns, k, n));
        _mockKubePluginWrapper.Setup(x => x.GetKubePodLogsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
           .Returns((string id, string ns, string pod, string c, int l) => _mockKubePlugin.GetKubePodLogsAsync(id, ns, pod, c, l));
        _mockKubePluginWrapper.Setup(x => x.GetCpuMetricsForWorkloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string id, string ns, string type, string name, string range) => _mockKubePlugin.GetCpuMetricsForWorkloadAsync(id, ns, type, name, range));
        _mockKubePluginWrapper.Setup(x => x.GetMemoryMetricsForWorkloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string id, string ns, string type, string name, string range) => _mockKubePlugin.GetMemoryMetricsForWorkloadAsync(id, ns, type, name, range));
        _mockKubePluginWrapper.Setup(x => x.DiagnoseAKSAppAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
          .Returns((string id, string ns, string k, string n) => _mockKubePlugin.DiagnoseAKSAppAsync(id, ns, k, n));
        // *** Crucial Setup for ScaleStatefulSetAsync ***
        _mockKubePluginWrapper.Setup(x => x.ScaleStatefulSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns((string id, string ns, string name, int replicas) =>
            {
                // Call the implementation AND potentially update its state
                return _mockKubePlugin.ScaleStatefulSetAsync(id, ns, name, replicas);
            });

        // Register the Mock<IKubePlugin>.Object, so the DI container provides the wrapper
        builder.Services.AddSingleton<IKubePlugin>(_mockKubePluginWrapper.Object);


        var sp = builder.Services.BuildServiceProvider();
        _host = builder.Build();

        _durableTaskClient = sp.GetRequiredService<DurableTaskClient>();
        _kubernetesAgentFactory = sp.GetRequiredService<KubernetesAgentFactory>();
        _threadRepository = _host.Services.GetRequiredService<IThreadRepository>();

        IChatClient client = _host.Services.GetRequiredService<IChatClient>();
        IEvaluationTokenCounter? tokenCounter = null;
        _chatConfiguration = new ChatConfiguration(client, tokenCounter);

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
            var results = await evalInput.EvaluateAgentResponsesAsync(fullHistory, tokenSource.Token);
            bool hasHighMatch = false;
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                if (result.Equivalence.Value >= 4)
                {
                    hasHighMatch = true;
                }
            }
            Assert.AreEqual(FormatAKSResourceId(_subscriptionId, _resourceGroupName, _aksClusterName), _mockKubePlugin.AksClusterResourceId, ignoreCase: true, $"AKS cluster resource ID is not as expected.");
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

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task AKSAgentDiagnoseSlowAppScaleRedis(Guid testRunGuid)
    {
        var tokenSource = new CancellationTokenSource();
        // Increase timeout for the longer scenario
        tokenSource.CancelAfter(TimeSpan.FromMinutes(7));
        EvalInput evalInput = new EvalInput(_chatConfiguration, this.TestContext, _llmDeploymentName);

        evalInput.GroundedContext = """
            ## Ground Truth:
            1. Agent receives an alert about a slow 'checkout' deployment.
            2. Agent identifies 'checkout' and its dependencies (including 'redis').
            3. Agent systematically analyzes components using DiagnoseAKSApp.
            4. Agent identifies **high CPU utilization** in 'redis' pods.
            5. Agent proposes **scaling the 'redis' StatefulSet** from 3 to 6 replicas.
            6. Agent **waits for and receives user approval**.
            7. Agent **executes the scaling action** (calls ScaleStatefulSetAsync).
            8. Agent monitors the scaling (implicitly in mock).
            9. Agent checks post-scaling metrics for 'redis' pods, showing stabilization/distribution.
            10. Agent concludes the incident, stating the scaling action resolved the issue.

            ## Expected Response Characteristics
            - Acknowledges the initial alert.
            - Lists dependencies.
            - Provides analysis summaries (identifying high Redis CPU).
            - Explicitly **proposes scaling redis** from 3 to 6.
            - **(Implicitly receives approval in this test flow)**
            - Confirms the scaling action was initiated/completed.
            - Provides **evidence of improvement** (post-scaling metrics/status).
            - States the issue is **resolved**.
            """;

        evalInput.ExampleResponse = """
            📊 Evidence of improvement: After scaling, **redis** now has 6 pods. All pods are Running/Ready, no errors in logs or events.

            Success criteria met:
            - CPU utilization now below 80% (target: <70% for all pods)
            - All 6 pods in Running state
            - No error spikes or abnormal events

            ✅ **RESOLVED**: The issue with checkout service slowness was caused by Redis CPU saturation. Scaling Redis has mitigated the problem. Would you like recommendations for future enhancements (e.g., auto-scaling, alerting)?
            """;

        var agentInput = $"""
        New PagerDuty Incident Reported
        Title: [#14] Checkout Service has become very slow
        Deployment Name: checkout
        Deployment Namespace: {_deploymentNamespace}
        AKS Cluster Name: {_aksClusterName}
        SubscriptionID: {_subscriptionId}
        ResourceGroup: {_resourceGroupName}
        Started at: 2025-04-25T10:05:54Z
        This is AKS workload issue, need to diagnose and quickly mitigate the issue.
        Incident ID: Q3TOZ5RFOAEP40
        Severity: high
        Source: PagerDuty
        """;

        string? instanceID = "";
        string aksResourceId = FormatAKSResourceId(_subscriptionId, _resourceGroupName, _aksClusterName);
        int initialRedisReplicas = 3;
        int targetRedisReplicas = 6;

        // Basic Cluster Info & Dependencies
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

        // Configure Dependency Graph
        _mocks.GraphDBPlugin.ConfigureAKSMicroservices(aksResourceId, _deploymentNamespace, "checkout", "checkout depends on [cart, currency, email, payment, product-catalog, shipping, kafka], cart depends on valkey-cart, product-catalog depends on redis (StatefulSet), shipping depends on quote");
        _mocks.GraphDBPlugin.ConfigureAKSMicroservices(aksResourceId, _deploymentNamespace, "cart", "cart depends on valkey-cart");
        _mocks.GraphDBPlugin.ConfigureAKSMicroservices(aksResourceId, _deploymentNamespace, "product-catalog", "product-catalog depends on redis (StatefulSet)");
        _mocks.GraphDBPlugin.ConfigureAKSMicroservices(aksResourceId, _deploymentNamespace, "shipping", "shipping depends on quote");

        // Mock Individual Component Diagnostics
        string healthySpecStatusYaml = """
            apiVersion: apps/v1
            kind: Deployment # or StatefulSet or Pod
            metadata:
              name: {name}
              namespace: default
            spec:
              replicas: 1 # Adjust for redis initial state
            status:
              conditions:
              - status: "True"
                type: Available
              - status: "True"
                type: Progressing
              readyReplicas: 1
              replicas: 1
            """;
        string healthyPodStatusYaml = """
            apiVersion: v1
            kind: Pod
            metadata:
              name: {podName}
              namespace: default
            spec:
               # ... spec details ...
            status:
              phase: Running
              conditions:
              - type: Initialized
                status: "True"
              - type: Ready
                status: "True"
              - type: ContainersReady
                status: "True"
              - type: PodScheduled
                status: "True"
              containerStatuses:
              - name: container-name
                ready: true
                restartCount: 0 # Adjust for email pod
                state:
                  running: {}
            """;
        string normalEvent = "[2025-04-25T10:00:00Z] Normal: Operation successful";

        // Helper to configure mocks for a standard healthy deployment
        Action<string, string> configureHealthyDeployment = (name, podName) =>
        {
            _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "apps/v1", "Deployment", name, healthySpecStatusYaml.Replace("{name}", name));
            _mockKubePlugin.ConfigurePodsForWorkload(aksResourceId, _deploymentNamespace, "Deployment", name, podName);
            _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "v1", "Pod", podName, healthyPodStatusYaml.Replace("{podName}", podName));
            _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "apps/v1", "Deployment", name, normalEvent);
            _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "", "Pod", podName, normalEvent); // Pod events
            _mockKubePlugin.ConfigureLogs(aksResourceId, _deploymentNamespace, podName, "Normal operations, no errors.");
            _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "Deployment", name, podName, cpuPercent: 5.0, memPercent: 15.0); // Generic low metrics
        };

        // Configure mocks for each component based on transcript summary
        configureHealthyDeployment("checkout", "checkout-7f4d74d64f-fwm8h");
        _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "Deployment", "checkout", "checkout-7f4d74d64f-fwm8h", cpuPercent: 0.79, memPercent: 6.09);
        _mockKubePlugin.ConfigureLogs(aksResourceId, _deploymentNamespace, "checkout-7f4d74d64f-fwm8h", "Normal order processing, payment, email, Kafka ops. Low latency. No errors. Last log: 2025-04-25T13:33:43Z");

        configureHealthyDeployment("cart", "cart-6bc5689bc9-nc8vd");
        _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "Deployment", "cart", "cart-6bc5689bc9-nc8vd", cpuPercent: 13.29, memPercent: 45.42);
        _mockKubePlugin.ConfigureLogs(aksResourceId, _deploymentNamespace, "cart-6bc5689bc9-nc8vd", "GetCartAsync/AddItemAsync calls normal. One anomalous log empty userId. No performance issues. Last log: 2025-04-25T13:35:37Z");

        configureHealthyDeployment("valkey-cart", "valkey-cart-dfb6ff45d-c69ds");
        _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "Deployment", "valkey-cart", "valkey-cart-dfb6ff45d-c69ds", cpuPercent: 2.37, memPercent: 7.22);
        _mockKubePlugin.ConfigureLogs(aksResourceId, _deploymentNamespace, "valkey-cart-dfb6ff45d-c69ds", "Regular successful background saves (RDB). No errors. Last log: 2025-04-25T13:36:21Z");

        // Currency - Healthy status, but logs show errors
        configureHealthyDeployment("currency", "currency-5687b668b5-gs5b6");
        _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "Deployment", "currency", "currency-5687b668b5-gs5b6", cpuPercent: 7.25, memPercent: 8.45);
        _mockKubePlugin.ConfigureLogs(aksResourceId, _deploymentNamespace, "currency-5687b668b5-gs5b6", "ERROR: OpenTelemetry OTLP gRPC exporter failed to connect to collector 10.0.223.231:4317 (connection refused). [Repeated >70 times]");

        // Email - Healthy
        configureHealthyDeployment("email", "email-c894688fb-9v8zf");
        _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "Deployment", "email", "email-c894688fb-9v8zf", cpuPercent: 0.65, memPercent: 64.10);
        _mockKubePlugin.ConfigureLogs(aksResourceId, _deploymentNamespace, "email-c894688fb-9v8zf", "Successful order confirmation email sends (0.003–0.010s). No errors. Last log: 2025-04-25T13:38:16Z");

        // Payment - Healthy
        configureHealthyDeployment("payment", "payment-66858d5869-ppwjm");
        _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "Deployment", "payment", "payment-66858d5869-ppwjm", cpuPercent: 0.21, memPercent: 15.14);
        _mockKubePlugin.ConfigureLogs(aksResourceId, _deploymentNamespace, "payment-66858d5869-ppwjm", "Successful transaction processing (<1ms latency). No errors. Last log: 2025-04-25T13:39:02Z");

        // Product Catalog - Healthy but log burst anomaly
        configureHealthyDeployment("product-catalog", "product-catalog-55db4bbf9f-s7psr");
        _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "Deployment", "product-catalog", "product-catalog-55db4bbf9f-s7psr", cpuPercent: 34.24, memPercent: 14.99);
        _mockKubePlugin.ConfigureLogs(aksResourceId, _deploymentNamespace, "product-catalog-55db4bbf9f-s7psr", "INFO: ListProducts called. [Repeated 80 times at 2025-04-25T13:39:59Z]. No errors or warnings.");

        // Shipping & Quote & Kafka (Assuming healthy)
        configureHealthyDeployment("shipping", "shipping-7f4d74d64f-fwm8h");
        configureHealthyDeployment("quote", "quote-6bc5689bc9-nc8vd");
        configureHealthyDeployment("kafka", "kafka-dfb6ff45d-c69ds");

        // Mock Redis (StatefulSet) - Initial State (High CPU)
        var redisPodNamesInitial = Enumerable.Range(0, initialRedisReplicas).Select(i => $"redis-{i}").ToList();
        _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "apps/v1", "StatefulSet", "redis",
            healthySpecStatusYaml
                .Replace("{name}", "redis")
                .Replace("kind: Deployment", "kind: StatefulSet")
                .Replace("replicas: 1", $"replicas: {initialRedisReplicas}")
                .Replace("readyReplicas: 1", $"readyReplicas: {initialRedisReplicas}"));
        _mockKubePlugin.ConfigurePodsForWorkload(aksResourceId, _deploymentNamespace, "StatefulSet", "redis", string.Join(", ", redisPodNamesInitial));
        _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "apps/v1", "StatefulSet", "redis", normalEvent); // Normal STS events
        foreach (var podName in redisPodNamesInitial)
        {
            _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "v1", "Pod", podName, healthyPodStatusYaml.Replace("{podName}", podName));
            _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "", "Pod", podName, normalEvent); // Normal Pod events
            _mockKubePlugin.ConfigureLogs(aksResourceId, _deploymentNamespace, podName, "Pod started cleanly, sync ok, AOF rewrite successful. No errors. (Maybe add old connection loss event from transcript if needed)");
        }

        // Configure High CPU Metrics (Initial State)
        _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "StatefulSet", "redis", "redis-0", cpuPercent: 98.28, memPercent: 8.0);
        _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "StatefulSet", "redis", "redis-1", cpuPercent: 96.90, memPercent: 8.1);
        _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "StatefulSet", "redis", "redis-2", cpuPercent: 95.28, memPercent: 8.2);

        // 4. Mock Redis Scaling Action & Post-Scaling State
        _mockKubePlugin.SetScalingCallback("redis", (targetReplicas) =>
        {
            Console.WriteLine($"Mock Scaling Callback: Redis scaling to {targetReplicas} triggered.");
            // Update internal state to reflect scaling for subsequent calls
            var redisPodNamesAfter = Enumerable.Range(0, targetReplicas).Select(i => $"redis-{i}").ToList();
            _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "apps/v1", "StatefulSet", "redis",
                healthySpecStatusYaml
                    .Replace("{name}", "redis")
                    .Replace("kind: Deployment", "kind: StatefulSet")
                    .Replace($"replicas: {initialRedisReplicas}", $"replicas: {targetReplicas}") // Update replica count in spec
                    .Replace($"readyReplicas: {initialRedisReplicas}", $"readyReplicas: {targetReplicas}")); // Update ready replicas
            _mockKubePlugin.ConfigurePodsForWorkload(aksResourceId, _deploymentNamespace, "StatefulSet", "redis", string.Join(", ", redisPodNamesAfter));
            // Add specs/events/logs for new pods
            for (int i = initialRedisReplicas; i < targetReplicas; i++)
            {
                var newPodName = $"redis-{i}";
                _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "v1", "Pod", newPodName, healthyPodStatusYaml.Replace("{podName}", newPodName));
                _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "", "Pod", newPodName, normalEvent);
                _mockKubePlugin.ConfigureLogs(aksResourceId, _deploymentNamespace, newPodName, "Pod started cleanly, initializing...");
            }

            // Update Metrics (Post Scaling - reflect transcript stabilization/new pod load)
            _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "StatefulSet", "redis", "redis-0", cpuPercent: 70.83, memPercent: 8.5); // Slightly higher?
            _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "StatefulSet", "redis", "redis-1", cpuPercent: 60.11, memPercent: 8.6); // Stabilized/Lower
            _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "StatefulSet", "redis", "redis-2", cpuPercent: 61.84, memPercent: 8.7); // Stabilized/Lower
            _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "StatefulSet", "redis", "redis-3", cpuPercent: 66.34, memPercent: 8.8); // Slightly higher?
            _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "StatefulSet", "redis", "redis-4", cpuPercent: 25.0, memPercent: 8.9); // Lower
            _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "StatefulSet", "redis", "redis-5", cpuPercent: 35.0, memPercent: 9.0); // Lower

        });

        try
        {
            var threadId = Guid.NewGuid();
            Console.WriteLine($"Starting Orchestration for test run {threadId}");
            instanceID = await _kubernetesAgentFactory.StartOrchestration(agentInput, threadId);
            Console.WriteLine($"Orchestration started with Instance ID: {instanceID}");

            await ApprovalTestHelper.DoApproval(
                durableTaskClient: _durableTaskClient,
                threadRepository: _threadRepository,
                threadId,
                logger: null,
                tokenSource.Token);

            // Continue with orchestration
            var orchestrationMetadata = await _durableTaskClient.WaitForInstanceCompletionAsync(instanceID, getInputsAndOutputs: true, tokenSource.Token);
            Console.WriteLine($"Orchestration {instanceID} completed with status: {orchestrationMetadata.RuntimeStatus}");
            Assert.IsTrue(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed, $"Orchestration failed with status {orchestrationMetadata.RuntimeStatus}. Details: {orchestrationMetadata.FailureDetails}");

            var fullHistory = orchestrationMetadata.ReadChatHistory();
            Assert.IsNotNull(fullHistory, "Chat history was null.");
            Assert.IsTrue(fullHistory.Length > 5, "Chat history seems too short."); // Basic sanity check

            Console.WriteLine("Evaluating agent responses...");
            var results = await evalInput.EvaluateAgentResponsesAsync(fullHistory, tokenSource.Token);
            Console.WriteLine($"Evaluation completed. {results.Count} responses evaluated.");

            bool hasHighMatch = results.Any(r => r.Equivalence.Value >= 4);

            // Verify key mock interactions
            Console.WriteLine("Verifying mock calls...");
            _mockKubePluginWrapper.Verify(x => x.GetAKSClusterResourceIdAsync(_subscriptionId, _resourceGroupName, _aksClusterName), Times.AtLeastOnce(), "GetAKSClusterResourceIdAsync was not called.");
            _mockKubePluginWrapper.Verify(x => x.DiagnoseAKSAppAsync(aksResourceId, _deploymentNamespace, "statefulset", "redis"), Times.AtLeastOnce(), "DiagnoseAKSAppAsync for redis was not called.");
            // Verify the scaling action was called correctly *after* approval
            _mockKubePluginWrapper.Verify(x => x.ScaleStatefulSetAsync(aksResourceId, _deploymentNamespace, "redis", targetRedisReplicas), Times.Once(), $"ScaleStatefulSetAsync was not called exactly once with {targetRedisReplicas} replicas.");

            Console.WriteLine("Assertions...");
            Assert.IsTrue(hasHighMatch, "No high equivalency result matched the example RESOLVED response, indicating the agent did not reach the correct conclusion or failed to report it as expected.");
            Console.WriteLine("Test Passed.");
        }
        catch (Grpc.Core.RpcException ex)
        {
            Assert.Fail($"Make sure you have the DTS emulator running (run-durable-emulator.ps1) or your appsettings.development.json has a valid Durable Task Scheduler connection string.{Environment.NewLine} {ex}");
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine($"Orchestration timed out. Instance ID: {instanceID}");
            if (!string.IsNullOrEmpty(instanceID))
            {
                try
                {
                    await _durableTaskClient.TerminateInstanceAsync(instanceID, new TerminateInstanceOptions { Output = "test cleanup on timeout", Recursive = true });
                }
                catch (Exception termEx)
                {
                    Console.WriteLine($"Error terminating instance {instanceID} after timeout: {termEx.Message}");
                }
            }
            Assert.Fail($"Orchestration timed out after {tokenSource.Token.WaitHandle.WaitOne(0)} ms. Exception: {ex}");
        }
        catch (Exception ex)
        {
            // General catch for unexpected issues during test execution or assertion failures within the try block.
            Console.WriteLine($"An unexpected error occurred: {ex}");
            Assert.Fail($"An unexpected error occurred during the test: {ex}");
        }
        finally
        {
            // Optional: Add any specific cleanup needed for this test if TestCleanup is not sufficient
        }
    }
}

