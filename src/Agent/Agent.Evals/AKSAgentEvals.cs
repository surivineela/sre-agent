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
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents;
using Moq;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Agent.Prometheus.Services;

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
        /* ===== Below section requires the appsettings to have corresponding configuration values ==== */
        // The easiest way to make it work is to use the same appsettings.json with your local development for Agent.Web.
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
        builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
        builder.Services.AddSingleton<IArmClientFactory, ArmClientFactory>().
                         AddSingleton<ArmHelper>().
                         AddSingleton<IArmPlugin, ArmPlugin>();
        // GremlinGraphDatabaseClient requires the appsettings to have GremlinGraphDb
        builder.Services.AddSingleton<IGraphDatabaseClient, GremlinGraphDatabaseClient>();
        builder.Services
            .AddTransient<IKubePlugin, KubePlugin>()
            .AddTransient<IChartPlugin, ChartPlugin>()
            .AddTransient<ChartPlugin>()
            .AddTransient<IGraphDBPlugin, GraphDBPlugin>();
        builder.Services
            .AddSingleton<IApprovalPlugin, ApprovalPlugin>()
            .AddSingleton<IGrafanaPlugin, GrafanaPlugin>();
        // We can use dts simulator to satisfy the durable task client below by:
        // docker run --rm -it --name dts-emulator -p 14280:8080 -p 14282:8082 -e ClientAuth__DisableAuthentication=true mcr.microsoft.com/dts/dts-emulator:v0.0.6
        builder.Services.AddDurableTaskWorker(b =>
        {
            b.AddTasks(r =>
            {
                DurableHelper.AddAllGeneratedTasks(r);
            });

            string durableConnectionString = builder.ResolveDtsConnectionString();
            b.UseDurableTaskScheduler(durableConnectionString);

            builder.Services.AddOptions<DurableTaskSchedulerWorkerOptions>(b.Name).Configure<IServiceProvider>((option, sp) =>
            {
                var authService = sp.GetRequiredService<IAuthenticationService>();
                var tokenCredential = authService.GetDtsCredential();

                option.Credential = tokenCredential;
            });
        });
        builder.Services.AddDurableTaskClient(b =>
        {
            string durableConnectionString = builder.ResolveDtsConnectionString();
            b.UseDurableTaskScheduler(durableConnectionString);

            builder.Services.AddOptions<DurableTaskSchedulerClientOptions>(b.Name).Configure<IServiceProvider>((option, sp) =>
            {
                var authService = sp.GetRequiredService<IAuthenticationService>();
                var tokenCredential = authService.GetDtsCredential();

                option.Credential = tokenCredential;
            });
        });
        /* ===== End of section that requires the appsettings to have corresponding configuration values ==== */
        builder.Services.AddSingleton<KubernetesAgentFactory>()
                        .AddSingleton<ArmHelper>()
                        .AddSingleton<IPrometheusQueryService, PrometheusQueryService>()
                        .AddSingleton<KubePluginDefinition>()
                        .AddSingleton<ChartPluginDefinition>()
                        .AddSingleton<GraphDBPluginDefinition>();

        AddMockService(builder.Services);

        builder.Services.AddArmHelperHttpClient();

        var sp = builder.Services.BuildServiceProvider();

        _durableTaskClient = sp.GetRequiredService<DurableTaskClient>();
        _kubernetesAgentFactory = sp.GetRequiredService<KubernetesAgentFactory>();

        _host = builder.Build();

        IChatClient client = _host.Services.GetRequiredService<IChatClient>();

        IEvaluationTokenCounter? tokenCounter = null;
        _chatConfiguration = new ChatConfiguration(client, tokenCounter);

        await _host.StartAsync();
    }

    // Below are the plugins not used in AKSAgent but required for ToolsRepository
    private void AddMockService(IServiceCollection services)
    {

        services.AddSingleton<ToolsRepository>()
            .AddSingleton<TimePluginDefinition>()
            .AddSingleton<MIConfigurationCheckPluginDefinition>()
            .AddSingleton(sp => new Mock<IMIConfigurationCheckPlugin>().Object)
            .AddSingleton<GithubWorkflowTriggerPluginDefinition>()
            .AddSingleton(sp => new Mock<IGithubWorkflowTriggerPlugin>().Object)
            .AddSingleton<RemediationPluginDefinition>()
            .AddSingleton(sp => new Mock<IRemediationPlugin>().Object)
            .AddSingleton<AppIdentityUpdatePluginDefinition>()
            .AddSingleton(sp => new Mock<IAppIdentityUpdatePlugin>().Object)
            .AddSingleton<ControlFlowPluginDefinition>()
            .AddSingleton<ApprovalPluginDefinition>()
            .AddSingleton(sp => new Mock<IApprovalPlugin>().Object)
            .AddSingleton<ContainerAppPluginDefinition>()
            .AddSingleton(sp => new Mock<IContainerAppPlugin>().Object)
            .AddSingleton<ReliabilityPluginDefinition>()
            .AddSingleton(sp => new Mock<IReliabilityPlugin>().Object)
            .AddSingleton<MetricsPluginDefinition>()
            .AddSingleton(sp => new Mock<IMetricsPlugin>().Object)
            .AddSingleton<RecordActionsPluginDefinition>()
            .AddSingleton(sp => new Mock<IRecordActionsPlugin>().Object)
            .AddSingleton<GrafanaPluginDefinition>()
            .AddSingleton(sp => new Mock<IGrafanaPlugin>().Object);

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

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task AKSAgentGenerateResourceGraph(Guid testRunGuid)
    {
        string groundedContext = """
            ## Ground Truth:
            1. Subscription ID, resource group, AKS cluster name, resource namespace and name are provided clearly.
            2. Agent can access to the AKS cluster by generating the resource ID from the information.
            3. Agent can generate the resource graph for the AKS cluster.

            ## Expected Response Characteristics
            - The response should clearly explain dependency relationships starting from the input component.
            - The response listed the component names, types (if not Deployment)
            """;

        var exampleResponse = $"""
            Here's the microservices topology relationship for the checkout deployment:
            checkout depends on [cart, currency, email, payment, product-catalog, shipping, kafka]
            cart depends on valkey-cart
            product-catalog depends on redis (StatefulSet)
            shipping depends on quote
            """;

        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMinutes(5));

        var deploymentName = "checkout";
        var input = $"""
        Can you draw a dependency graph for the following components in the AKS cluster?
        - Subscription ID: {_subscriptionId}
        - Resource Group: {_resourceGroupName}
        - AKS Cluster Name: {_aksClusterName}
        - Deployment Namespace: {_deploymentNamespace}
        - Deployment Name: {deploymentName}
        """;
        string? instanceID = "";

        try
        {
            instanceID = await _kubernetesAgentFactory.StartOrchestration(input, new ThreadContext(testRunGuid, AgentTypeEnum.DTS));

            // Create a background thread to poll messages via threadRepository
            var threadRepository = _host.Services.GetRequiredService<IThreadRepository>();
            var pollingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(tokenSource.Token);

            // Create a thread-safe queue to pass messages from background thread to main thread
            var messageQueue = new System.Collections.Concurrent.ConcurrentQueue<string>();

            // Force output flushing
            Console.SetError(new System.IO.StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
            Console.SetOut(new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });

            Console.WriteLine($"CONSOLE: Test starting {testRunGuid}");

            Task pollingTask = Task.Run(async () =>
            {
                try
                {
                    DateTime MaxTimestamp = DateTime.MinValue;
                    while (!pollingCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        // Poll messages from the thread repository for the current thread
                        var messages = await threadRepository.GetMessagesAsync(testRunGuid);
                        if (messages != null && messages.Any())
                        {
                            foreach (var msg in messages)
                            {
                                if (msg.TimeStamp <= MaxTimestamp)
                                    continue;

                                Console.WriteLine($"Message: {msg.Author}: {msg.Text}");
                            }

                            if (messages.Any())
                                MaxTimestamp = messages.Max(m => m.TimeStamp);
                        }

                        // Reduced polling interval to get more frequent updates
                        await Task.Delay(TimeSpan.FromMilliseconds(500), pollingCancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DEBUG ERROR: {ex.Message}");
                    Console.WriteLine($"Error in polling thread: {ex}");
                    messageQueue.Enqueue($"ERROR in polling thread: {ex}");
                }
            }, pollingCancellationTokenSource.Token);

            // Continue with orchestration
            var orchestrationMetadata = await _durableTaskClient.WaitForInstanceCompletionAsync(instanceID, getInputsAndOutputs: true, tokenSource.Token);

            // Clean up
            pollingCancellationTokenSource.Cancel();

            // Rest of your existing code...
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
                    var result = await response.EvaluateAsync(this.TestContext, this._chatConfiguration, messagesSoFar, groundedContext, exampleResponse, _llmDeploymentName);
                    // var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                    // string jsonResult = System.Text.Json.JsonSerializer.Serialize(result, jsonOptions);
                    // Console.WriteLine($"Result: {jsonResult}");
                }
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

