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

// Implementation of the AKS Agent test for app down scenario
public partial class AKSAgentEvals
{
    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task AKSAgentDiagnoseAppDownThrowException(Guid testRunGuid)
    {
        var tokenSource = new CancellationTokenSource();
        // Increase timeout for the longer scenario
        tokenSource.CancelAfter(TimeSpan.FromMinutes(3));
        EvalInput evalInput = new EvalInput(_chatConfiguration, this.TestContext, _llmDeploymentName);

        evalInput.GroundedContext = """
            ## Ground Truth:
            1. Agent receives a direct request to mitigate issues with the 'cart' deployment.
            2. Agent identifies that cart pods are failing with "VALKEY_ADDR environment variable is required" error.
            3. Agent lists revisions of the cart deployment and compares them.
            4. Agent identifies that the latest revision is missing the VALKEY_ADDR environment variable.
            5. Agent determines that the VALKEY_ADDR value should be "valkey-cart:6379" based on a previous revision.
            6. Agent proposes **patching the deployment with the missing environment variable**.
            7. Agent **waits for and receives user approval**.
            8. Agent **executes the patching action** (calls PatchKubernetesYamlAsync).
            9. Agent monitors the availability metrics after the patch.
            10. Agent concludes the incident, stating that the patching action resolved the issue.

            ## Expected Response Characteristics
            - Acknowledges the issue with the deployment cart.
            - **(Implicitly receives approval in this test flow)**
            - Confirms the patching action was performed successfully.
            - Provides **evidence of improvement** (availability metrics back to 100%).
            - States the issue is **resolved**.
            """;

        evalInput.ExampleResponse = """
            📊 Evidence of improvement: After patching the deployment with the missing VALKEY_ADDR environment variable, the **cart** service is now:
            - Pods are Running/Ready with no crash loops
            - No errors in logs
            - Availability back to 100% (from 0%)

            Success criteria met:
            - All pods in Running state
            - No error logs about missing VALKEY_ADDR
            - Service availability restored to 100%

            ✅ **RESOLVED**: The issue with deployment cart being down was caused by a missing environment variable (VALKEY_ADDR=valkey-cart:6379) in the latest deployment. Adding this environment variable has resolved the crash loop and restored service availability. Would you like recommendations to prevent similar issues in the future?
            """;
        var agentInput = $"""
        Can you mitigate the issue that deployment cart is broken in my AKS cluster?

        My cluster details:
        - Subscription ID: {_subscriptionId}
        - Resource Group: {_resourceGroupName}
        - AKS Cluster Name: {_aksClusterName}
        - Namespace: {_deploymentNamespace}
        """;

        string? instanceID = "";
        string aksResourceId = FormatAKSResourceId(_subscriptionId, _resourceGroupName, _aksClusterName);

        // Basic Cluster Info & Dependencies
        _mockKubePlugin.ConfigureNamespaces(
            FormatAKSResourceId(_subscriptionId, _resourceGroupName, _aksClusterName),
            "default, kube-system, kube-public");
        _mockKubePlugin.ConfigureDeployments(
            FormatAKSResourceId(_subscriptionId, _resourceGroupName, _aksClusterName),
            _deploymentNamespace,
            "cart, valkey-cart");
        _mockKubePlugin.ConfigureStatefulSets(
            FormatAKSResourceId(_subscriptionId, _resourceGroupName, _aksClusterName),
            _deploymentNamespace,
            "redis");

        // Configure Dependency Graph
        _mocks.GraphDBPlugin.ConfigureAKSMicroservices(aksResourceId, _deploymentNamespace, "cart", "cart depends on valkey-cart");

        // Mock Individual Component Diagnostics
        string healthySpecStatusYaml = """
            apiVersion: apps/v1
            kind: Deployment # or StatefulSet or Pod
            metadata:
              name: {name}
              namespace: default
            spec:
              replicas: 1
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
                restartCount: 0
                state:
                  running: {}
            """;
        string normalEvent = "[2025-05-12T08:00:00Z] Normal: Operation successful";
        string crashLoopEvent = "[2025-05-12T08:10:15Z] Warning: BackOff: Back-off restarting failed container";

        // Helper to configure mocks for a standard healthy deployment
        Action<string, string> configureHealthyDeployment = (name, podName) =>
        {
            _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "apps/v1", "Deployment", name, healthySpecStatusYaml.Replace("{name}", name));
            _mockKubePlugin.ConfigurePodsForWorkload(aksResourceId, _deploymentNamespace, "Deployment", name, podName);
            _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "v1", "Pod", podName, healthyPodStatusYaml.Replace("{podName}", podName));
            _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "apps/v1", "Deployment", name, normalEvent);
            _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "", "Pod", podName, normalEvent); // Pod events
            _mockKubePlugin.ConfigureLogs(aksResourceId, _deploymentNamespace, podName, "Normal operations, no errors.");
            _mockKubePlugin.ConfigureWorkloadMetrics(aksResourceId, _deploymentNamespace, "Deployment", name, cpuPercent: 5.0, memPercent: 15.0, availPercent: 100.0); // Generic low metrics
        };

        // Configure CrashLoopBackOff for Cart deployment
        string cartCrashLoopSpecStatus = """
            apiVersion: apps/v1
            kind: Deployment
            metadata:
              name: cart
              namespace: default
            spec:
              replicas: 1
              template:
                spec:
                  containers:
                  - name: cart
                    image: ghcr.io/retaildevcrews/cart:latest
                    env:
                    - name: ASPNETCORE_ENVIRONMENT
                      value: Development
                    # Missing VALKEY_ADDR environment variable
            status:
              conditions:
              - status: "False"
                type: Available
                message: "Deployment does not have minimum availability."
              - status: "True"
                type: Progressing
                message: "ReplicaSet has less than desired number of pods running."
              readyReplicas: 0
              replicas: 1
            """;

        string cartPodCrashLoopStatus = """
            apiVersion: v1
            kind: Pod
            metadata:
              name: cart-7d94d59b76-xfw2p
              namespace: default
              labels:
                app: cart
            spec:
              containers:
              - name: cart
                image: ghcr.io/retaildevcrews/cart:latest
                env:
                - name: ASPNETCORE_ENVIRONMENT
                  value: Development
                # Missing VALKEY_ADDR environment variable
            status:
              phase: Running
              conditions:
              - type: Initialized
                status: "True"
              - type: Ready
                status: "False"
                message: "Container cart is not ready: Container is in CrashLoopBackOff state"
              - type: ContainersReady
                status: "False"
              - type: PodScheduled
                status: "True"
              containerStatuses:
              - name: cart
                ready: false
                restartCount: 5
                state:
                  waiting:
                    reason: CrashLoopBackOff
                    message: "Back-off restarting failed container"
                lastState:
                  terminated:
                    reason: Error
                    exitCode: 1
                    finishedAt: "2025-05-12T08:14:55Z"
            """;

        configureHealthyDeployment("valkey-cart", "valkey-cart-dfb6ff45d-c69ds");

        // Configure the cart deployment as failing
        _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "apps/v1", "Deployment", "cart", cartCrashLoopSpecStatus);
        _mockKubePlugin.ConfigurePodsForWorkload(aksResourceId, _deploymentNamespace, "Deployment", "cart", "cart-7d94d59b76-xfw2p");
        _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "v1", "Pod", "cart-7d94d59b76-xfw2p", cartPodCrashLoopStatus);
        _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "apps/v1", "Deployment", "cart", "[2025-05-12T08:10:05Z] Warning: FailedCreate: Error creating pod cart-7d94d59b76-xfw2p");
        _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "", "Pod", "cart-7d94d59b76-xfw2p", crashLoopEvent);
        _mockKubePlugin.ConfigureLogs(aksResourceId, _deploymentNamespace, "cart-7d94d59b76-xfw2p", "Error: VALKEY_ADDR environment variable is required.\nSystem.ArgumentNullException: Value cannot be null. (Parameter 'VALKEY_ADDR')\n   at Microsoft.Extensions.Configuration.ConfigurationExtensions.GetRequiredValue(IConfiguration configuration, String key)\n   at CartService.Startup.ConfigureServices(IServiceCollection services)");
        _mockKubePlugin.ConfigureWorkloadMetrics(aksResourceId, _deploymentNamespace, "Deployment", "cart", cpuPercent: 0.0, memPercent: 0.0, availPercent: 0.0);

        // Configure deployment revisions
        string previousRevision = """
            {
              "kind": "Deployment",
              "apiVersion": "apps/v1",
              "metadata": {
                "name": "cart",
                "namespace": "default",
                "labels": {
                  "app": "cart"
                },
                "annotations": {
                  "deployment.kubernetes.io/revision": "1"
                }
              },
              "spec": {
                "replicas": 1,
                "selector": {
                  "matchLabels": {
                    "app": "cart"
                  }
                },
                "template": {
                  "metadata": {
                    "labels": {
                      "app": "cart"
                    }
                  },
                  "spec": {
                    "containers": [
                      {
                        "name": "cart",
                        "image": "ghcr.io/retaildevcrews/cart:v1.0.3",
                        "env": [
                          {
                            "name": "VALKEY_ADDR",
                            "value": "valkey-cart:6379"
                          },
                          {
                            "name": "ASPNETCORE_ENVIRONMENT",
                            "value": "Development"
                          }
                        ],
                        "ports": [
                          {
                            "containerPort": 8080
                          }
                        ]
                      }
                    ]
                  }
                }
              },
              "status": {
                "availableReplicas": 1,
                "readyReplicas": 1,
                "replicas": 1
              }
            }
            """;

        string currentRevision = """
            {
              "kind": "Deployment",
              "apiVersion": "apps/v1",
              "metadata": {
                "name": "cart",
                "namespace": "default",
                "labels": {
                  "app": "cart"
                },
                "annotations": {
                  "deployment.kubernetes.io/revision": "2"
                }
              },
              "spec": {
                "replicas": 1,
                "selector": {
                  "matchLabels": {
                    "app": "cart"
                  }
                },
                "template": {
                  "metadata": {
                    "labels": {
                      "app": "cart"
                    }
                  },
                  "spec": {
                    "containers": [
                      {
                        "name": "cart",
                        "image": "ghcr.io/retaildevcrews/cart:v1.0.4",
                        "env": [
                          {
                            "name": "ASPNETCORE_ENVIRONMENT",
                            "value": "Development"
                          }
                        ],
                        "ports": [
                          {
                            "containerPort": 8080
                          }
                        ]
                      }
                    ]
                  }
                }
              },
              "status": {
                "availableReplicas": 0,
                "readyReplicas": 0,
                "replicas": 1
              }
            }
            """;

        _mockKubePlugin.ConfigureDeploymentRevisions(aksResourceId, _deploymentNamespace, "cart", $"[{currentRevision}, {previousRevision}]");
          // Mock the patching functionality
        bool patchApplied = false;
        _mockKubePluginWrapper.Setup(x => x.PatchKubernetesYamlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string resourceId, string yamlContent) =>
            {
                // Check if the patch includes the VALKEY_ADDR environment variable
                if (yamlContent.Contains("VALKEY_ADDR") && yamlContent.Contains("valkey-cart:6379"))
                {
                    patchApplied = true;
                    Console.WriteLine("Mock: Patch with VALKEY_ADDR environment variable was applied.");

                    // Update cart deployment to be healthy after patch
                    _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "apps/v1", "Deployment", "cart", healthySpecStatusYaml.Replace("{name}", "cart"));
                    _mockKubePlugin.ConfigurePodsForWorkload(aksResourceId, _deploymentNamespace, "Deployment", "cart", "cart-8e95f7c84-ab12c");
                    _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "v1", "Pod", "cart-8e95f7c84-ab12c", healthyPodStatusYaml.Replace("{podName}", "cart-8e95f7c84-ab12c"));
                    _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "apps/v1", "Deployment", "cart", normalEvent);
                    _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "", "Pod", "cart-8e95f7c84-ab12c", normalEvent);
                    _mockKubePlugin.ConfigureLogs(aksResourceId, _deploymentNamespace, "cart-8e95f7c84-ab12c", "deployment cart started successfully. Connected to valkey at valkey-cart:6379");
                    _mockKubePlugin.ConfigureWorkloadMetrics(aksResourceId, _deploymentNamespace, "Deployment", "cart", cpuPercent: 1.0, memPercent: 10.0, availPercent: 100.0);

                    return "Deployment 'cart' patched successfully. Rollout in progress.";
                }
                else
                {
                    return "Error: Patch did not include required environment variable VALKEY_ADDR=valkey-cart:6379";
                }
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
            _mockKubePluginWrapper.Verify(x => x.ListWorkloadRevisions(aksResourceId, _deploymentNamespace, "deployment", "cart"), Times.AtLeastOnce(), "ListWorkloadRevisions for cart was not called.");
            // Verify that the patching was performed
            Assert.IsTrue(patchApplied, "The patching operation was not performed correctly with VALKEY_ADDR environment variable.");

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
