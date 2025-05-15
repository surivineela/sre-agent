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
    public async Task AKSAgentDiagnoseAppDownBadDeployment(Guid testRunGuid)
    {
        var tokenSource = new CancellationTokenSource();
        // Increase timeout for the longer scenario
        tokenSource.CancelAfter(TimeSpan.FromMinutes(3));
        EvalInput evalInput = new EvalInput(_chatConfiguration, this.TestContext, _llmDeploymentName);

        evalInput.GroundedContext = """
            ## Ground Truth:
            1. Agent receives a direct request to mitigate issues with the 'product-catalog' deployment.
            2. Agent identifies that product-catalog pods are in CrashLoopBackOff state with panic errors in the logs.
            3. Agent lists revisions of the product-catalog deployment and compares them.
            4. Agent identifies that the latest revision is using an incorrect image "demo-app-product-catalog:v2".
            5. Agent determines that the correct image should be "demo-app-product-catalog:v1" based on a previous revision.
            6. Agent proposes **patching the deployment with the correct image**.
            7. Agent **waits for and receives user approval**.
            8. Agent **executes the patching action** (calls PatchKubernetesYamlAsync).
            9. Agent monitors the availability metrics after the patch.
            10. Agent concludes the incident, stating that the patching action resolved the issue.

            ## Expected Response Characteristics
            - Acknowledges the issue with the deployment product-catalog.
            - **(Implicitly receives approval in this test flow)**
            - Confirms the patching action was performed successfully.
            - Provides **evidence of improvement** (availability metrics back to 100%).
            - States the issue is **resolved**.
            """;

        evalInput.ExampleResponse = """
            📊 Evidence of improvement: After patching the deployment with the correct image, the **product-catalog** service is now:
            - Pods are Running/Ready with no crash loops
            - No errors in logs
            - Availability back to 100% (from 0%)

            Success criteria met:
            - All pods in Running state
            - No crash loop errors
            - Service availability restored to 100%

            ✅ **RESOLVED**: The issue with deployment product-catalog being down was caused by an incorrect image reference (demo-app-product-catalog:v2) in the latest deployment. Updating to the correct image (demo-app-product-catalog:v1) has resolved the issue and restored service availability. Would you like recommendations to prevent similar issues in the future?
            """;
        var agentInput = $"""
        Can you mitigate the issue that deployment product-catalog is broken in my AKS cluster?

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
            "product-catalog");


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
        string crashLoopEvent = "[2025-05-12T08:10:15Z] Warning: BackOff: Back-off restarting failed container product-catalog in pod product-catalog-7d94d59b76-xfw2p";

        // Configure CrashLoopBackOff for product-catalog deployment
        string productCatalogFailingSpecStatus = """
            apiVersion: apps/v1
            kind: Deployment
            metadata:
              name: product-catalog
              namespace: default
            spec:
              replicas: 1
              template:
                spec:
                  containers:
                  - name: product-catalog
                    image: demo-app-product-catalog:v2
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

        string productCatalogPodFailingStatus = """
            apiVersion: v1
            kind: Pod
            metadata:
              name: product-catalog-7d94d59b76-xfw2p
              namespace: default
              labels:
                app: product-catalog
            spec:
              containers:
              - name: product-catalog
                image: demo-app-product-catalog:v2
            status:
              phase: Running
              conditions:
              - type: Initialized
                status: "True"
              - type: Ready
                status: "False"
                message: "Container product-catalog is not ready: container restarted 5 times"
              - type: ContainersReady
                status: "False"
              - type: PodScheduled
                status: "True"
              containerStatuses:
              - name: product-catalog
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


        // Configure the product-catalog deployment as failing
        _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "apps/v1", "Deployment", "product-catalog", productCatalogFailingSpecStatus);
        _mockKubePlugin.ConfigurePodsForWorkload(aksResourceId, _deploymentNamespace, "Deployment", "product-catalog", "product-catalog-7d94d59b76-xfw2p");
        _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "v1", "Pod", "product-catalog-7d94d59b76-xfw2p", productCatalogPodFailingStatus);
        _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "apps/v1", "Deployment", "product-catalog", "[2025-05-12T08:10:05Z] Warning: FailedCreate: Container product-catalog is continuously crashing");
        _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "", "Pod", "product-catalog-7d94d59b76-xfw2p", crashLoopEvent);
        _mockKubePlugin.ConfigureLogs(aksResourceId, _deploymentNamespace, "product-catalog-7d94d59b76-xfw2p", "panic: productcatalog service internal error\n\ngoroutine 47 [running]:\nmain.(*productCatalog).ListProducts(0x18a1840?, {0x11510d8?, 0xc000193ef0?}, 0x49ea65?)\n        /src/app/main.go:332 +0x166");

        // Configure metrics for product-catalog service (0% availability)
        _mockKubePlugin.ConfigureWorkloadMetrics(aksResourceId, _deploymentNamespace, "Deployment", "product-catalog", cpuPercent: 0.0, memPercent: 0.0, availPercent: 0.0);

        // Configure deployment revisions
        string previousRevision = """
            {
              "kind": "Deployment",
              "apiVersion": "apps/v1",
              "metadata": {
                "name": "product-catalog",
                "namespace": "default",
                "labels": {
                  "app": "product-catalog"
                },
                "annotations": {
                  "deployment.kubernetes.io/revision": "1"
                }
              },
              "spec": {
                "replicas": 1,
                "selector": {
                  "matchLabels": {
                    "app": "product-catalog"
                  }
                },
                "template": {
                  "metadata": {
                    "labels": {
                      "app": "product-catalog"
                    }
                  },
                  "spec": {
                    "containers": [
                      {
                        "name": "product-catalog",
                        "image": "demo-app-product-catalog:v1",
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
                "name": "product-catalog",
                "namespace": "default",
                "labels": {
                  "app": "product-catalog"
                },
                "annotations": {
                  "deployment.kubernetes.io/revision": "2"
                }
              },
              "spec": {
                "replicas": 1,
                "selector": {
                  "matchLabels": {
                    "app": "product-catalog"
                  }
                },
                "template": {
                  "metadata": {
                    "labels": {
                      "app": "product-catalog"
                    }
                  },
                  "spec": {
                    "containers": [
                      {
                        "name": "product-catalog",
                        "image": "demo-app-product-catalog:v2",
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

        _mockKubePlugin.ConfigureDeploymentRevisions(aksResourceId, _deploymentNamespace, "product-catalog", $"[{currentRevision}, {previousRevision}]");

        // Mock the patching functionality
        bool patchApplied = false;
        _mockKubePluginWrapper.Setup(x => x.PatchKubernetesYamlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string resourceId, string yamlContent) =>
            {
                // Check if the patch includes the correct image
                if (yamlContent.Contains("demo-app-product-catalog:v1"))
                {
                    patchApplied = true;
                    Console.WriteLine("Mock: Patch with correct image was applied.");

                    // Update product-catalog deployment to be healthy after patch
                    _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "apps/v1", "Deployment", "product-catalog", healthySpecStatusYaml.Replace("{name}", "product-catalog"));
                    _mockKubePlugin.ConfigurePodsForWorkload(aksResourceId, _deploymentNamespace, "Deployment", "product-catalog", "product-catalog-8e95f7c84-ab12c");
                    _mockKubePlugin.ConfigureSpecStatus(aksResourceId, _deploymentNamespace, "v1", "Pod", "product-catalog-8e95f7c84-ab12c", healthyPodStatusYaml.Replace("{podName}", "product-catalog-8e95f7c84-ab12c"));
                    _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "apps/v1", "Deployment", "product-catalog", normalEvent);
                    _mockKubePlugin.ConfigureEvents(aksResourceId, _deploymentNamespace, "", "Pod", "product-catalog-8e95f7c84-ab12c", normalEvent);
                    _mockKubePlugin.ConfigureLogs(aksResourceId, _deploymentNamespace, "product-catalog-8e95f7c84-ab12c", "Product catalog service started successfully.");
                    _mockKubePlugin.ConfigureWorkloadMetrics(aksResourceId, _deploymentNamespace, "Deployment", "product-catalog", cpuPercent: 5.0, memPercent: 15.0, availPercent: 100.0);

                    return "Deployment 'product-catalog' patched successfully. Rollout in progress.";
                }
                else
                {
                    return "Error: Patch did not include the correct image reference 'demo-app-product-catalog:v1'";
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
            _mockKubePluginWrapper.Verify(x => x.ListWorkloadRevisions(aksResourceId, _deploymentNamespace, "deployment", "product-catalog"), Times.AtLeastOnce(), "ListWorkloadRevisions for product-catalog was not called.");
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
