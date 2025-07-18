// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Tests.Common;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace Agent.Evals;

[TestClass]
public class CliExecutionHelperEvals
{
    private static TestHost TestHost { get; } = TestHelpers.InitializeTestHost();

    public static IEnumerable<object[]> TestCases => new[]
    {
        new object[] { "kubectl_describe_pod_crashed_container", GetKubectlDescribePodCrashedOutput(), CliErrorType.None, "kubectl describe pod with crashed container - command succeeded but shows unhealthy resource state" },
        new object[] { "kubectl_get_deployment_not_ready", GetKubectlGetDeploymentNotReadyOutput(), CliErrorType.None, "kubectl get deployment showing 0/1 ready - command succeeded but shows unhealthy resource state" },
        new object[] { "kubectl_get_pods_crashloop", GetKubectlGetPodsCrashLoopOutput(), CliErrorType.None, "kubectl get pods with CrashLoopBackOff - command succeeded but shows unhealthy resource state" },
        new object[] { "kubectl_get_events_errors", GetKubectlGetEventsWithErrorsOutput(), CliErrorType.None, "kubectl get events showing error events - command succeeded but shows resource problems" },
        new object[] { "kubectl_resource_not_found", GetKubectlResourceNotFoundOutput(), CliErrorType.NotFoundError, "kubectl resource not found - command execution error" },
        new object[] { "kubectl_permission_denied", GetKubectlPermissionDeniedOutput(), CliErrorType.AuthorizationError, "kubectl permission denied - command execution error" },
        new object[] { "kubectl_authentication_required", GetKubectlAuthenticationRequiredOutput(), CliErrorType.AuthorizationError, "kubectl authentication required - command execution error" },
        new object[] { "kubectl_connection_refused", GetKubectlConnectionRefusedOutput(), CliErrorType.Other, "kubectl connection refused - command execution error" },
        new object[] { "azure_cli_vm_deallocated", GetAzureCliVmDeallocatedOutput(), CliErrorType.None, "Azure CLI showing deallocated VM - command succeeded but shows unhealthy resource state" },
        new object[] { "azure_cli_login_required", GetAzureCliLoginRequiredOutput(), CliErrorType.AuthorizationError, "Azure CLI login required - command execution error" },
        new object[] { "azure_cli_resource_not_found", GetAzureCliResourceNotFoundOutput(), CliErrorType.NotFoundError, "Azure CLI resource not found - command execution error" },
        new object[] { "azure_cli_validation_error", GetAzureCliValidationErrorOutput(), CliErrorType.ValidationError, "Azure CLI validation error - command execution error" }
    };

    [TestMethod]
    [DynamicData(nameof(TestCases))]
    public async Task CliExecutionHelper_ShouldCorrectlyClassifyCommandOutputs(string testName, string output, CliErrorType expectedErrorType, string description)
    {
        // Arrange
        var chatClient = TestHost.RunConfig.ChatClient;
        
        // Act
        var result = await CliExecutionHelper.ParseCliExecutionResult(chatClient, output);

        // Assert
        Assert.IsNotNull(result, $"Result should not be null for test: {testName}");
        Assert.AreEqual(output, result.Output, $"Output should match input for test: {testName}");
        Assert.AreEqual(expectedErrorType, result.ErrorType, $"Error type should be {expectedErrorType} for test: {testName} - {description}");
        Assert.AreEqual(expectedErrorType != CliErrorType.None, result.ErrorOccurred, $"ErrorOccurred should be {expectedErrorType != CliErrorType.None} for test: {testName}");
    }

    [TestMethod]
    public async Task CliExecutionHelper_ShouldHandleRealWorldComplexOutputs()
    {
        // Arrange
        var chatClient = TestHost.RunConfig.ChatClient;
        var complexOutputs = new[]
        {
            GetKubectlDescribePodCrashedOutput(),
            GetKubectlGetDeploymentNotReadyOutput(),
            GetKubectlGetPodsCrashLoopOutput()
        };

        foreach (var output in complexOutputs)
        {
            // Act
            var result = await CliExecutionHelper.ParseCliExecutionResult(chatClient, output);

            // Assert
            Assert.IsNotNull(result, "Result should not be null for complex output");
            Assert.AreEqual(CliErrorType.None, result.ErrorType, "Complex resource status outputs should not be classified as command errors");
            Assert.IsFalse(result.ErrorOccurred, "ErrorOccurred should be false for successful commands showing unhealthy resource states");
        }
    }

    [TestMethod]
    public async Task CliExecutionHelper_ShouldHandleKubectlDescribePodWithCrashedContainer()
    {
        // Arrange
        var chatClient = TestHost.RunConfig.ChatClient;
        var realWorldOutput = GetKubectlDescribePodCrashedOutput();

        // Act
        var result = await CliExecutionHelper.ParseCliExecutionResult(chatClient, realWorldOutput);

        // Assert
        Assert.IsNotNull(result, "Result should not be null");
        Assert.AreEqual(CliErrorType.None, result.ErrorType, "kubectl describe pod with crashed container should not be classified as command error - the command succeeded but shows unhealthy resource state");
        Assert.IsFalse(result.ErrorOccurred, "ErrorOccurred should be false - command executed successfully");
    }

    [TestMethod]
    public async Task CliExecutionHelper_ShouldHandleKubectlGetDeploymentNotReady()
    {
        // Arrange
        var chatClient = TestHost.RunConfig.ChatClient;
        var realWorldOutput = GetKubectlGetDeploymentNotReadyOutput();

        // Act
        var result = await CliExecutionHelper.ParseCliExecutionResult(chatClient, realWorldOutput);

        // Assert
        Assert.IsNotNull(result, "Result should not be null");
        Assert.AreEqual(CliErrorType.None, result.ErrorType, "kubectl get deployment showing 0/1 ready should not be classified as command error - the command succeeded but shows unhealthy resource state");
        Assert.IsFalse(result.ErrorOccurred, "ErrorOccurred should be false - command executed successfully");
    }

    [TestMethod]
    public async Task CliExecutionHelper_ShouldDistinguishBetweenCommandErrorsAndResourceStatus()
    {
        // Arrange
        var chatClient = TestHost.RunConfig.ChatClient;
        
        // Test successful command with unhealthy resource state
        var healthyResourceOutput = GetKubectlDescribePodCrashedOutput();
        var healthyResult = await CliExecutionHelper.ParseCliExecutionResult(chatClient, healthyResourceOutput);
        
        // Test actual command execution error
        var commandErrorOutput = GetKubectlResourceNotFoundOutput();
        var errorResult = await CliExecutionHelper.ParseCliExecutionResult(chatClient, commandErrorOutput);

        // Assert
        Assert.AreEqual(CliErrorType.None, healthyResult.ErrorType, "Command succeeded but resource is unhealthy - not a command error");
        Assert.IsFalse(healthyResult.ErrorOccurred, "Command executed successfully");
        
        Assert.AreEqual(CliErrorType.NotFoundError, errorResult.ErrorType, "Command failed due to resource not found - this is a command error");
        Assert.IsTrue(errorResult.ErrorOccurred, "Command execution failed");
    }

    // Test data methods
    private static string GetKubectlDescribePodCrashedOutput() => @"
Name:         product-catalog-6ccfdd68f-ldl2v
Namespace:    default
Priority:     0
Node:         aks-nodepool1-42814270-vmss000009/10.224.0.11
Start Time:   Fri, 18 Jul 2025 14:17:13 +0800
Labels:       app.kubernetes.io/component=product-catalog
              app.kubernetes.io/instance=opentelemetry-demo
              app.kubernetes.io/name=product-catalog
              opentelemetry.io/name=product-catalog
              pod-template-hash=6ccfdd68f
Annotations:  kubectl.kubernetes.io/restartedAt: 2025-05-28T19:10:33+08:00
Status:       Running
IP:           10.244.3.210
IPs:
  IP:           10.244.3.210
Controlled By:  ReplicaSet/product-catalog-6ccfdd68f
Containers:
  product-catalog:
    Container ID:   containerd://e4859baf6facd213fdc7e871bd34ac782579ecabe6051b71509454172e2ddbbd
    Image:          fatsheep9146/otel-demo:latest-product-catalog-18
    Image ID:       docker.io/fatsheep9146/otel-demo@sha256:8f0fdf617b96f500afe2c7a0a5d117f87c0dfc9cb7b7e7c5da70d09304bc45cb
    Port:           8080/TCP
    Host Port:      0/TCP
    State:          Terminated
      Reason:       Error
      Exit Code:    2
      Started:      Fri, 18 Jul 2025 14:34:24 +0800
      Finished:     Fri, 18 Jul 2025 14:34:49 +0800
    Last State:     Terminated
      Reason:       Error
      Exit Code:    2
      Started:      Fri, 18 Jul 2025 14:29:14 +0800
      Finished:     Fri, 18 Jul 2025 14:29:20 +0800
    Ready:          False
    Restart Count:  8
    Limits:
      cpu:     500m
      memory:  200Mi
    Requests:
      cpu:     500m
      memory:  200Mi
    Environment:
      OTEL_SERVICE_NAME:                                   (v1:metadata.labels['app.kubernetes.io/component'])
      OTEL_COLLECTOR_NAME:                                otel-collector
      OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE:  cumulative
      REDIS_ADDRESS:                                      redis:6379
      REDIS_PASSWORD:                                     redispassword
      PRODUCT_CATALOG_PORT:                               8080
      REDIS_LIST_RANGE_LIMIT:                             1000
      PRODUCT_CATALOG_RELOAD_INTERVAL:                    10
      FLAGD_HOST:                                         flagd
      FLAGD_PORT:                                         8013
      OTEL_EXPORTER_OTLP_ENDPOINT:                        http://$(OTEL_COLLECTOR_NAME):4317
      OTEL_RESOURCE_ATTRIBUTES:                           service.name=$(OTEL_SERVICE_NAME),service.namespace=opentelemetry-demo,service.version=2.0.0
    Mounts:
      /usr/src/app/products from product-catalog-products (rw)
      /var/run/secrets/kubernetes.io/serviceaccount from kube-api-access-9rk9d (ro)
Conditions:
  Type                        Status
  PodReadyToStartContainers   True 
  Initialized                 True 
  Ready                       False 
  ContainersReady             False 
  PodScheduled                True 
Volumes:
  product-catalog-products:
    Type:      ConfigMap (a volume populated by a ConfigMap)
    Name:      product-catalog-products
    Optional:  false
  kube-api-access-9rk9d:
    Type:                    Projected (a volume that contains injected data from multiple sources)
    TokenExpirationSeconds:  3607
    ConfigMapName:           kube-root-ca.crt
    ConfigMapOptional:       <nil>
    DownwardAPI:             true
QoS Class:                   Guaranteed
Node-Selectors:              <none>
Tolerations:                 node.kubernetes.io/memory-pressure:NoSchedule op=Exists
                             node.kubernetes.io/not-ready:NoExecute op=Exists for 300s
                             node.kubernetes.io/unreachable:NoExecute op=Exists for 300s
Events:
  Type     Reason     Age                   From               Message
  ----     ------     ----                  ----               -------
  Normal   Scheduled  17m                   default-scheduler  Successfully assigned default/product-catalog-6ccfdd68f-ldl2v to aks-nodepool1-42814270-vmss000009
  Normal   Pulled     15m (x5 over 17m)     kubelet            Container image ""fatsheep9146/otel-demo:latest-product-catalog-18"" already present on machine
  Normal   Created    15m (x5 over 17m)     kubelet            Created container: product-catalog
  Normal   Started    15m (x5 over 17m)     kubelet            Started container product-catalog
  Warning  BackOff    2m48s (x64 over 17m)  kubelet            Back-off restarting failed container product-catalog in pod product-catalog-6ccfdd68f-ldl2v_default(af9443ce-ad82-46c8-b5b0-469a244416de)
";

    private static string GetKubectlGetDeploymentNotReadyOutput() => @"
NAME              READY   UP-TO-DATE   AVAILABLE   AGE     CONTAINERS        IMAGES                                             SELECTOR
product-catalog   0/1     1            0           4m30s   product-catalog   fatsheep9146/otel-demo:latest-product-catalog-18   opentelemetry.io/name=product-catalog
";

    private static string GetKubectlGetPodsCrashLoopOutput() => @"
NAME                     READY   STATUS             RESTARTS        AGE
nginx-deployment-abc123  0/1     CrashLoopBackOff   5 (2m ago)     10m
web-app-def456          1/1     Running            0               5m
product-catalog-xyz789   0/1     Error              3 (1m ago)     8m
";

    private static string GetKubectlGetEventsWithErrorsOutput() => @"
LAST SEEN   TYPE     REASON      OBJECT                       MESSAGE
2m          Warning  Failed      pod/product-catalog-xyz789   Failed to pull image ""nginx:invalid-tag""
1m          Warning  BackOff     pod/product-catalog-xyz789   Back-off pulling image ""nginx:invalid-tag""
30s         Warning  Unhealthy   pod/product-catalog-xyz789   Readiness probe failed: connection refused
";

    private static string GetKubectlResourceNotFoundOutput() => @"
Error from server (NotFound): pods ""non-existent-pod"" not found
";

    private static string GetKubectlPermissionDeniedOutput() => @"
Error from server (Forbidden): pods is forbidden: User ""system:serviceaccount:default:default"" cannot list resource ""pods"" in API group """" in the namespace ""default""
";

    private static string GetKubectlAuthenticationRequiredOutput() => @"
error: You must be logged in to the server (Unauthorized)
";

    private static string GetKubectlConnectionRefusedOutput() => @"
The connection to the server localhost:8080 was refused - did you specify the right host or port?
";

    private static string GetAzureCliVmDeallocatedOutput() => @"
{
  ""id"": ""/subscriptions/12345/resourceGroups/test-rg/providers/Microsoft.Compute/virtualMachines/test-vm"",
  ""name"": ""test-vm"",
  ""powerState"": ""VM deallocated"",
  ""provisioningState"": ""Succeeded"",
  ""hardwareProfile"": {
    ""vmSize"": ""Standard_D2s_v3""
  },
  ""storageProfile"": {
    ""imageReference"": {
      ""publisher"": ""Canonical"",
      ""offer"": ""0001-com-ubuntu-server-focal"",
      ""sku"": ""20_04-lts-gen2"",
      ""version"": ""latest""
    }
  }
}
";

    private static string GetAzureCliLoginRequiredOutput() => @"
ERROR: You need to log in to access your subscriptions. Please run 'az login'.
";

    private static string GetAzureCliResourceNotFoundOutput() => @"
ERROR: The resource 'myresource' was not found in resource group 'test-rg'.
";

    private static string GetAzureCliValidationErrorOutput() => @"
ERROR: Invalid resource group name. Resource group names only allow alphanumeric characters, periods, underscores, hyphens and parenthesis and cannot end in a period.
";
}
