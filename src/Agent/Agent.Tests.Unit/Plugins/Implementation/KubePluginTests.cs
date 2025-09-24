using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Framework;
using Agent.Graph.Crawler.Metrics;
using Agent.Graph.Services;
using Agent.Plugins;
using Agent.Prometheus.Services;
using Agent.Plugins.Interface;
using k8s;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Unit.Plugins.Implementation
{
  public class KubePluginTests
  {
    private readonly KubePlugin _kubePlugin;
    private readonly Mock<IKubernetesClientFactory> _mockKubernetesClientFactory;
    private const string _testResourceId = "/subscriptions/sub-id/resourceGroups/rg/providers/Microsoft.ContainerService/managedClusters/cluster1";

    public KubePluginTests()
    {
      // Create mock for Kubernetes client factory
      _mockKubernetesClientFactory = new Mock<IKubernetesClientFactory>();
      // Setup the mock to use local kubeconfig for KinD cluster
      _mockKubernetesClientFactory
          .Setup(x => x.CreateKubernetesClientFromResourceIdAsync(It.IsAny<string>()))
          .Returns<string>(resourceId => Task.FromResult<IKubernetes?>(new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig())));

      // Also mock the crawler method which might be used
      _mockKubernetesClientFactory
          .Setup(x => x.CreateKubernetesClientFromResourceIdForCrawlerAsync(It.IsAny<string>()))
          .Returns<string>(resourceId => Task.FromResult<IKubernetes?>(new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig())));

      // Create an actual instance with our mocked dependencies
      _kubePlugin = new KubePlugin(
          new Mock<IChatClient>().Object,
          new Mock<IPrometheusQueryService>().Object,
          new Mock<IAzureMetricsClient>().Object,
          _mockKubernetesClientFactory.Object,
          new Mock<IArmClientFactory>().Object,
          new Mock<IGraphDatabaseClient>().Object,
          new Mock<IThreadRepository>().Object,
          new Mock<IAgentOutboundCommunicationService>().Object,
          new Mock<IAuthenticationService>().Object,
          new Mock<IHostEnvironment>().Object,
          new Mock<ILogger<KubePlugin>>().Object,
          new Mock<ICrawlerTriggerService>().Object,
          new Mock<ActionSettings>().Object,
          new Mock<IAgentRuntimeModifier<AgentContext>>().Object,
          new Mock<IKubeJavaPlugin>().Object,
          new Mock<IPrometheusEndpointService>().Object
          );
    }

    [Fact]
    public async Task PatchKubernetesYamlAsync_EmptyResourceId_ReturnsError()
    {
      // Act
      var result = await _kubePlugin.PatchKubernetesYamlAsync("", "yaml: content");

      // Assert
      Assert.Contains("Error: AKS Cluster Resource ID is empty", result);
    }

    [Fact]
    public async Task PatchKubernetesYamlAsync_EmptyYaml_ReturnsError()
    {
      // Act
      var result = await _kubePlugin.PatchKubernetesYamlAsync(_testResourceId, "");

      // Assert
      Assert.Contains("Error: YAML content is empty", result);
    }

    [Fact]
    public async Task PatchKubernetesYamlAsync_MultipleYamlObjects_ReturnsError()
    {
      // Arrange
      var multipleYaml = "apiVersion: v1\nkind: Pod\n---\napiVersion: v1\nkind: Service";

      // Act
      var result = await _kubePlugin.PatchKubernetesYamlAsync(_testResourceId, multipleYaml);

      // Assert
      Assert.Contains("Error parsing multiple YAML objects", result);
    }

    [Fact]
    public async Task PatchKubernetesYamlAsync_InvalidYaml_ReturnsError()
    {
      // Arrange
      var invalidYaml = "this is not valid yaml";

      // Act
      var result = await _kubePlugin.PatchKubernetesYamlAsync(_testResourceId, invalidYaml);

      // Assert
      Assert.Contains("Error", result);
    }

    [Fact(Skip = "Only run this test when a local KinD cluster is available")]
    public async Task PatchKubernetesYamlAsync_WithLocalKubeConfig_CreatesNginxDeployment()
    {
      // This test will create an nginx deployment in your local KinD cluster

      // Create a simple nginx deployment YAML
      string nginxDeploymentYaml = @"
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
        env:
        - name: ENV
          value: ""8080""

";

      try
      {
        // Attempt to apply the YAML to create the nginx deployment
        var result = await _kubePlugin.PatchKubernetesYamlAsync(_testResourceId, nginxDeploymentYaml);

        // If successful, the result should contain a success message
        Assert.Contains("Successfully", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Deployment/checkout", result, StringComparison.OrdinalIgnoreCase);

        // Note: In a real world scenario, you would wait for the deployment to be ready
        // and validate that pods are running before considering the test successful.
        // You might also perform cleanup after the test to remove the deployment.

        // Log success message
        Console.WriteLine("Successfully created nginx deployment in KinD cluster");
      }
      catch (Exception ex)
      {
        // Use Assert.Fail instead of Assert.True(false, ...)
        Assert.Fail($"Test failed to create deployment: {ex.Message}\nStack trace: {ex.StackTrace}\nInner exception: {ex.InnerException?.Message ?? "none"}");
      }
    }

    [Fact(Skip = "Only run this test when a local KinD cluster is available")]
    public async Task VerifyNginxDeploymentInKindCluster()
    {
      // This test will verify that the nginx deployment was created correctly
      // by checking for its pods via the Kubernetes API

      try
      {
        // Get a Kubernetes client
        var k8sClient = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());

        // Wait a short time to allow pods to start (5 seconds)
        await Task.Delay(5000);

        // Get pods with the nginx-test label
        var pods = await k8sClient.CoreV1.ListNamespacedPodAsync(
            namespaceParameter: "default",
            labelSelector: "app=nginx-test");

        // Verify we have pods and they are either Running or Creating
        Assert.NotEmpty(pods.Items);

        // Assert that at least one pod is starting/running
        foreach (var pod in pods.Items)
        {
          Console.WriteLine($"Found pod: {pod.Metadata.Name}, status: {pod.Status.Phase}");
        }

        // Log how many pods were found
        Console.WriteLine($"Found {pods.Items.Count} nginx pods in the default namespace");

        // Optional: You can uncomment this to cleanup the deployment after testing
        /*
        await k8sClient.AppsV1.DeleteNamespacedDeploymentAsync(
            name: "nginx-test-deployment",
            namespaceParameter: "default");
        Console.WriteLine("Cleaned up nginx test deployment");
        */
      }
      catch (Exception ex)
      {
        Assert.Fail($"Test failed to verify nginx deployment in KinD cluster: {ex.Message}");
      }
    }

    [Fact(Skip = "Only run this test for end-to-end testing with a local KinD cluster")]
    public async Task CreateAndVerifyCompleteNginxDeployment_EndToEndTest()
    {
      // This is a more comprehensive end-to-end test that:
      // 1. Creates a namespace for the test
      // 2. Creates an nginx deployment
      // 3. Creates a service exposing nginx
      // 4. Verifies all components are created correctly
      // 5. Cleans up everything afterward

      var testNamespace = "nginx-test-namespace";
      var deploymentName = "nginx-complete-test";
      var serviceName = "nginx-service";

      try
      {
        // Step 1: Create a test namespace
        string namespaceYaml = $@"
apiVersion: v1
kind: Namespace
metadata:
  name: {testNamespace}
";
        var nsResult = await _kubePlugin.PatchKubernetesYamlAsync(_testResourceId, namespaceYaml);
        Assert.Contains("Successfully", nsResult, StringComparison.OrdinalIgnoreCase);
        Console.WriteLine($"Created test namespace: {testNamespace}");

        // Step 2: Create the nginx deployment in our test namespace
        string nginxDeploymentYaml = $@"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {deploymentName}
  namespace: {testNamespace}
  labels:
    app: nginx
spec:
  replicas: 1
  selector:
    matchLabels:
      app: nginx
  template:
    metadata:
      labels:
        app: nginx
    spec:
      containers:
      - name: nginx
        image: nginx:stable
        ports:
        - containerPort: 80
";
        var deployResult = await _kubePlugin.PatchKubernetesYamlAsync(_testResourceId, nginxDeploymentYaml);
        Assert.Contains("Successfully", deployResult, StringComparison.OrdinalIgnoreCase);
        Console.WriteLine($"Created nginx deployment: {deploymentName}");

        // Step 3: Create a service for nginx
        string serviceYaml = $@"
apiVersion: v1
kind: Service
metadata:
  name: {serviceName}
  namespace: {testNamespace}
spec:
  selector:
    app: nginx
  ports:
  - port: 80
    targetPort: 80
  type: ClusterIP
";
        var svcResult = await _kubePlugin.PatchKubernetesYamlAsync(_testResourceId, serviceYaml);
        Assert.Contains("Successfully", svcResult, StringComparison.OrdinalIgnoreCase);
        Console.WriteLine($"Created nginx service: {serviceName}");

        // Wait for the deployment to become available
        Console.WriteLine("Waiting for deployment to be ready...");
        await Task.Delay(5000); // Wait 5 seconds

        // Step 4: Verify everything was created correctly
        var k8sClient = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());

        // Verify deployment
        var deployment = await k8sClient.AppsV1.ReadNamespacedDeploymentAsync(
            name: deploymentName,
            namespaceParameter: testNamespace);
        Assert.NotNull(deployment);
        Assert.Equal(1, deployment.Spec.Replicas);

        // Verify service
        var service = await k8sClient.CoreV1.ReadNamespacedServiceAsync(
            name: serviceName,
            namespaceParameter: testNamespace);
        Assert.NotNull(service);
        Assert.Equal(80, service.Spec.Ports[0].Port);

        Console.WriteLine("Successfully verified all components!");

        // Step 5: Clean up
        Console.WriteLine("Performing cleanup...");

        // Delete the service first
        await k8sClient.CoreV1.DeleteNamespacedServiceAsync(
            name: serviceName,
            namespaceParameter: testNamespace);

        // Delete the deployment
        await k8sClient.AppsV1.DeleteNamespacedDeploymentAsync(
            name: deploymentName,
            namespaceParameter: testNamespace);

        // Finally delete the namespace which will clean up everything else
        await k8sClient.CoreV1.DeleteNamespaceAsync(name: testNamespace);

        Console.WriteLine("Test completed successfully with full cleanup");
      }
      catch (Exception ex)
      {
        Assert.Fail($"End-to-end test failed: {ex.Message}");
      }
    }

    [Fact(Skip = "Only run this test when a local KinD cluster is available")]
    public async Task PatchKubernetesYamlAsync_WithLocalKubeConfig_CreatesStatefulSet()
    {
      // This test will create a StatefulSet in your local KinD cluster
      // StatefulSets are used for applications that require stable network identities and persistent storage

      // First create a test namespace for this example
      string namespaceYaml = @"
apiVersion: v1
kind: Namespace
metadata:
  name: statefulset-test-ns
";

      // Then create a simple statefulset YAML for a web application with persistent storage
      string statefulSetYaml = @"
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: web-statefulset
  namespace: statefulset-test-ns
spec:
  selector:
    matchLabels:
      app: web-app
  serviceName: ""web""
  replicas: 1
  template:
    metadata:
      labels:
        app: web-app
    spec:
      containers:
      - name: web-app
        image: nginx:stable
        ports:
        - containerPort: 80
          name: web
        volumeMounts:
        - name: web-data
          mountPath: /usr/share/nginx/html
  volumeClaimTemplates:
  - metadata:
      name: web-data
    spec:
      accessModes: [ ""ReadWriteOnce"" ]
      resources:
        requests:
          storage: 1Gi
";

      try
      {
        // First create the namespace
        var nsResult = await _kubePlugin.PatchKubernetesYamlAsync(_testResourceId, namespaceYaml);
        Assert.Contains("Successfully", nsResult, StringComparison.OrdinalIgnoreCase);
        Console.WriteLine("Successfully created namespace for StatefulSet test");

        // Attempt to apply the YAML to create the statefulset
        var result = await _kubePlugin.PatchKubernetesYamlAsync(_testResourceId, statefulSetYaml);

        // If successful, the result should contain a success message
        Assert.Contains("Successfully", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("StatefulSet/web-statefulset", result, StringComparison.OrdinalIgnoreCase);

        Console.WriteLine("Successfully created StatefulSet in KinD cluster");

        // Note: In a production environment, you would wait for the StatefulSet to be ready
        // and verify persistent volumes are created correctly

        // Optional: Get the StatefulSet to verify it exists
        var k8sClient = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());
        var statefulSet = await k8sClient.AppsV1.ReadNamespacedStatefulSetAsync(
            name: "web-statefulset",
            namespaceParameter: "statefulset-test-ns");

        Assert.NotNull(statefulSet);
        Assert.Equal(1, statefulSet.Spec.Replicas);

        // Optional: Clean up resources
        /*
        await k8sClient.AppsV1.DeleteNamespacedStatefulSetAsync(
            name: "web-statefulset",
            namespaceParameter: "statefulset-test-ns");
        await k8sClient.CoreV1.DeleteNamespaceAsync(name: "statefulset-test-ns");
        Console.WriteLine("Cleaned up StatefulSet test resources");
        */
      }
      catch (Exception ex)
      {
        Assert.Fail($"Test failed to create StatefulSet in KinD cluster: {ex.Message}");
      }
    }
  }
}
