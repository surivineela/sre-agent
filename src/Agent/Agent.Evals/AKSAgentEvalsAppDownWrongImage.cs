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

// Implementation of the AKS Agent test for app down scenario with wrong image
public partial class AKSAgentEvals
{
    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task AKSAgentDiagnoseAppDownWrongImage(Guid testRunGuid)
    {
        var tokenSource = new CancellationTokenSource();
        // Increase timeout for the longer scenario
        tokenSource.CancelAfter(TimeSpan.FromMinutes(7));
        EvalInput evalInput = new EvalInput(_chatConfiguration, this.TestContext, _llmDeploymentName);

        evalInput.GroundedContext = """
            ## Ground Truth:
            1. Agent receives a direct request to mitigate issues with the 'checkout' deployment.
            2. Agent identifies that checkout pods are failing with "Failed to pull image" error for image "demo-app-checkout:wrong".
            3. Agent lists revisions of the checkout deployment and compares them.
            4. Agent identifies that the latest revision is using an incorrect image "demo-app-checkout:wrong".
            5. Agent determines that the correct image should be "demo-app-checkout:correct" based on a previous revision.
            6. Agent proposes **patching the deployment with the correct image**.
            7. Agent **waits for and receives user approval**.
            8. Agent **executes the patching action** (calls PatchKubernetesYamlAsync).
            9. Agent monitors the availability metrics after the patch.
            10. Agent concludes the incident, stating that the patching action resolved the issue.

            ## Expected Response Characteristics
            - Acknowledges the issue with the deployment checkout.
            - **(Implicitly receives approval in this test flow)**
            - Confirms the patching action was performed successfully.
            - Provides **evidence of improvement** (availability metrics back to 100%).
            - States the issue is **resolved**.
            """;

        evalInput.ExampleResponse = """
            📊 Evidence of improvement: After patching the deployment with the correct image, the **checkout** service is now:
            - Pods are Running/Ready with no crash loops or ImagePullBackOff issues
            - No errors in logs
            - Availability back to 100% (from 0%)

            Success criteria met:
            - All pods in Running state
            - No image pull errors
            - Service availability restored to 100%

            ✅ **RESOLVED**: The issue with deployment checkout being down was caused by an incorrect image reference (demo-app-checkout:wrong) in the latest deployment. Updating to the correct image (demo-app-checkout:correct) has resolved the issue and restored service availability. Would you like recommendations to prevent similar issues in the future?
            """;
        var agentInput = $"""
        Can you mitigate the issue that deployment checkout is broken in my AKS cluster?

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
            "checkout, demo-app");
        _mockKubePlugin.ConfigureStatefulSets(
            FormatAKSResourceId(_subscriptionId, _resourceGroupName, _aksClusterName),
            _deploymentNamespace,
            "redis");

        // Configure Dependency Graph
        _mocks.GraphDBPlugin.ConfigureAKSMicroservices(aksResourceId, _deploymentNamespace, "checkout", "checkout depends on demo-app");

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
        string imagePullBackoffEvent = "[2025-05-12T08:10:15Z] Warning: Failed: Failed to pull image \"demo-app-checkout:wrong\": rpc error: code = NotFound desc = failed to pull and unpack image \"demo-app-checkout:wrong\": failed to resolve reference \"demo-app-checkout:wrong\": demo-app-checkout:wrong: not found";

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

        // Configure ImagePullBackOff for Checkout deployment
        string checkoutFailingSpecStatus = """
            apiVersion: apps/v1
            kind: Deployment
            metadata:
              name: checkout
              namespace: default
            spec:
              replicas: 1
              template:
                spec:
                  containers:
                  - name: checkout
                    image: demo-app-checkout:wrong
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

        string checkoutPodFailingStatus = """
            apiVersion: v1
            kind: Pod
            metadata:
              name: checkout-7d94d59b76-xfw2p
              namespace: default
              labels:
                app: checkout
            spec:
              containers:
              - name: checkout
                image: demo-app-checkout:wrong
            status:
              phase: Pending
              conditions:
              - type: Initialized
                status: "True"
              - type: Ready
                status: "False"
                message: "Container checkout is not ready: Image pull failed"
              - type: ContainersReady
                status: "False"
              - type: PodScheduled
                status: "True"
              containerStatuses:
              - name: checkout
                ready: false
                restartCount: 3
                state:
                  waiting:
                    reason: ImagePullBackOff
                    message: "Back-off pulling image \"demo-app-checkout:wrong\""
                lastState:
                  terminated:
                    reason: Error
                    exitCode: 1
                    finishedAt: "2025-05-12T08:14:55Z"
            """;

        configureHealthyDeployment("demo-app", "demo-app-dfb6ff45d-c69ds");

        // Configure the checkout deployment as failing
        _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "apps/v1", "Deployment", "checkout", checkoutFailingSpecStatus);
        _mockKubePlugin.ConfigurePodsForWorkload(aksResourceId, _deploymentNamespace, "Deployment", "checkout", "checkout-7d94d59b76-xfw2p");
        _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "v1", "Pod", "checkout-7d94d59b76-xfw2p", checkoutPodFailingStatus);
        _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "apps/v1", "Deployment", "checkout", "[2025-05-12T08:10:05Z] Warning: FailedCreate: Error creating pod checkout-7d94d59b76-xfw2p");
        _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "", "Pod", "checkout-7d94d59b76-xfw2p", imagePullBackoffEvent);
        _mockKubePlugin.ConfigureLogs(aksResourceId, _deploymentNamespace, "checkout-7d94d59b76-xfw2p", "Container is in ImagePullBackOff state. Unable to pull image.");

        // Configure metrics for checkout service (0% availability)
        _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "Deployment", "checkout", "checkout-7d94d59b76-xfw2p", cpuPercent: 0.0, memPercent: 0.0);

        // Configure deployment revisions
        string previousRevision = """
            {
              "kind": "Deployment",
              "apiVersion": "apps/v1",
              "metadata": {
                "name": "checkout",
                "namespace": "default",
                "labels": {
                  "app": "checkout"
                },
                "annotations": {
                  "deployment.kubernetes.io/revision": "1"
                }
              },
              "spec": {
                "replicas": 1,
                "selector": {
                  "matchLabels": {
                    "app": "checkout"
                  }
                },
                "template": {
                  "metadata": {
                    "labels": {
                      "app": "checkout"
                    }
                  },
                  "spec": {
                    "containers": [
                      {
                        "name": "checkout",
                        "image": "demo-app-checkout:correct",
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
                "name": "checkout",
                "namespace": "default",
                "labels": {
                  "app": "checkout"
                },
                "annotations": {
                  "deployment.kubernetes.io/revision": "2"
                }
              },
              "spec": {
                "replicas": 1,
                "selector": {
                  "matchLabels": {
                    "app": "checkout"
                  }
                },
                "template": {
                  "metadata": {
                    "labels": {
                      "app": "checkout"
                    }
                  },
                  "spec": {
                    "containers": [
                      {
                        "name": "checkout",
                        "image": "demo-app-checkout:wrong",
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

        _mockKubePlugin.ConfigureDeploymentRevisions(aksResourceId, _deploymentNamespace, "checkout", $"[{currentRevision}, {previousRevision}]");

        // Mock the patching functionality
        bool patchApplied = false;
        _mockKubePluginWrapper.Setup(x => x.PatchKubernetesYamlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string resourceId, string yamlContent) =>
            {
                // Check if the patch includes the correct image
                if (yamlContent.Contains("demo-app-checkout:correct"))
                {
                    patchApplied = true;
                    Console.WriteLine("Mock: Patch with correct image was applied.");

                    // Update checkout deployment to be healthy after patch
                    _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "apps/v1", "Deployment", "checkout", healthySpecStatusYaml.Replace("{name}", "checkout"));
                    _mockKubePlugin.ConfigurePodsForWorkload(aksResourceId, _deploymentNamespace, "Deployment", "checkout", "checkout-8e95f7c84-ab12c");
                    _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "v1", "Pod", "checkout-8e95f7c84-ab12c", healthyPodStatusYaml.Replace("{podName}", "checkout-8e95f7c84-ab12c"));
                    _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "apps/v1", "Deployment", "checkout", normalEvent);
                    _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "", "Pod", "checkout-8e95f7c84-ab12c", normalEvent);
                    _mockKubePlugin.ConfigureLogs(aksResourceId, _deploymentNamespace, "checkout-8e95f7c84-ab12c", "Checkout service started successfully.");
                    _mockKubePlugin.ConfigureMetrics(aksResourceId, _deploymentNamespace, "Deployment", "checkout", "checkout-8e95f7c84-ab12c", cpuPercent: 5.0, memPercent: 15.0);

                    return "Deployment 'checkout' patched successfully. Rollout in progress.";
                }
                else
                {
                    return "Error: Patch did not include the correct image reference 'demo-app-checkout:correct'";
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

            bool hasHighMatch = results.Any(r => r.Equivalence?.Value >= 4);

            // Verify key mock interactions
            Console.WriteLine("Verifying mock calls...");
            _mockKubePluginWrapper.Verify(x => x.GetAKSClusterResourceIdAsync(_subscriptionId, _resourceGroupName, _aksClusterName), Times.AtLeastOnce(), "GetAKSClusterResourceIdAsync was not called.");
            _mockKubePluginWrapper.Verify(x => x.ListWorkloadRevisions(aksResourceId, _deploymentNamespace, "deployment", "checkout"), Times.AtLeastOnce(), "ListWorkloadRevisions for checkout was not called.");
            // Verify that the patching was performed
            Assert.IsTrue(patchApplied, "The patching operation was not performed correctly with the correct image.");

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
