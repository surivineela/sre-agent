// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using Agent.Core.Helpers;
using k8s;
using k8s.Models;
using System.Text.Json;
using Newtonsoft.Json;
using YamlDotNet.Serialization;
using Newtonsoft.Json.Converters;
using System.Dynamic;
using System.Collections.Concurrent;
using Azure.ResourceManager;
using Azure.Core;
using Azure.ResourceManager.ContainerService;
using System.Text;
using System.Text.RegularExpressions;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;

namespace Agent.Plugins
{
    public class KubePlugin : IKubePlugin
    {
        private readonly ILogger? _logger;
        private IKubernetes _client;
        private IChatClient _chatClient;

        private readonly IAuthenticationService _authService;

        private ThreadContext Context { get; set; }
        private readonly ConcurrentDictionary<string, IKubernetes> _clientCache = new();
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(60);
        private readonly ConcurrentDictionary<string, DateTimeOffset> _cacheTimestamps = new();

        public KubePlugin(IConfiguration configuration, IAuthenticationService authenticationService, IChatClient chatClient, ILogger<KubePlugin>? logger)
        {
            _logger = logger;
            _authService = authenticationService;
            _chatClient = chatClient;
        }

        public async Task<IKubernetes> GetOrCreateClientAsync(string? resourceId = null)
        {
            // If no resourceId is provided, use the default client
            if (string.IsNullOrEmpty(resourceId))
            {
                if (_client == null)
                {
                    throw new InvalidOperationException(
                        "No default Kubernetes client available. Please provide an AKS resource ID.");
                }
                return _client;
            }

            // Check if we have a cached client for this resourceId
            if (_clientCache.TryGetValue(resourceId, out var cachedClient))
            {
                // Check if the cache has expired
                if (_cacheTimestamps.TryGetValue(resourceId, out var timestamp) &&
                    DateTimeOffset.UtcNow - timestamp < _cacheExpiration)
                {
                    _logger?.LogDebug("Using cached Kubernetes client for resourceId: {ResourceId}", resourceId);
                    return cachedClient;
                }
                // Cache expired, remove it
                _clientCache.TryRemove(resourceId, out _);
                _cacheTimestamps.TryRemove(resourceId, out _);
            }

            // Create ARM client
            var credential = _authService.GetCrawlerCredential();
            var armClient = new ArmClient(credential);

            // Get the AKS cluster
            var resourceIdentifier = new ResourceIdentifier(resourceId);
            var managedCluster = armClient.GetContainerServiceManagedClusterResource(resourceIdentifier);

            var credentialsResponse = await managedCluster.GetAccessProfileAsync("clusterAdmin");

            // The KubeConfig is returned as a byte array that needs proper decoding
            byte[] kubeConfigBytes = credentialsResponse.Value.KubeConfig;
            string kubeConfig = null;

            if (kubeConfigBytes != null && kubeConfigBytes.Length > 0)
            {
                // Properly decode the byte array to a string
                kubeConfig = System.Text.Encoding.UTF8.GetString(kubeConfigBytes);
            }

            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, kubeConfig);
            try
            {
                var k8sConfig = KubernetesClientConfiguration.BuildConfigFromConfigFile(tempFile);
                var client = new Kubernetes(k8sConfig);
                // Cache the client
                _clientCache[resourceId] = client;
                _cacheTimestamps[resourceId] = DateTimeOffset.UtcNow;

                return client;
            }
            finally
            {
                try { File.Delete(tempFile); } catch { /* ignore cleanup errors */ }
            }
        }

        public async Task<string> GetAKSClusterResourceIdAsync(string Subscription, string ResourceGroupName, string AKSClusterName)
        {
            return $"AKSClusterResourceID is **'/subscriptions/{Subscription}/resourceGroups/{ResourceGroupName}/providers/Microsoft.ContainerService/managedClusters/{AKSClusterName}'**";
        }

        // get all namespaces in the cluster 
        public async Task<string> GetKubeNamespacesAsync(string resourceId)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            var namespaces = await _client.CoreV1.ListNamespaceAsync();
            var namespaceNames = namespaces.Items.Select(ns => ns.Metadata.Name);

            return string.Join(", ", namespaceNames);
        }

        // get all deployments in a namespace
        public async Task<string> GetKubeDeploymentsAsync(string resourceId, string _namespace)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            var deployments = await _client.AppsV1.ListNamespacedDeploymentAsync(_namespace);
            var deploymentNames = deployments.Items.Select(deployment => deployment.Metadata.Name);

            return string.Join(", ", deploymentNames);
        }

        // get all statefulsets in a namespace
        public async Task<string> GetKubeStatefulsetsAsync(string resourceId, string _namespace)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            var statefulSetList = await _client.AppsV1.ListNamespacedStatefulSetAsync(_namespace);
            var statefulSetNames = statefulSetList.Items.Select(sts => sts.Metadata.Name);

            return string.Join(", ", statefulSetNames);
        }

        // get pods of a deployment in a namespace
        public async Task<string> GetKubePodsAsync(string resourceId, string _namespace, string deployment)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            // get deployment in namespace
            var deploy = await _client.AppsV1.ReadNamespacedDeploymentAsync(deployment, _namespace);
            IDictionary<string, string> labels = null;
            if (deploy == null)
            {
                // TODO: add GetKubePodsForStatefulSetAsync to get pods of a statefulset
                if (deployment == "redis")
                {
                    // Fallback to redis deployment if specified
                    var sts = await _client.AppsV1.ReadNamespacedStatefulSetAsync("redis", _namespace);
                    labels = sts.Spec.Template.Metadata.Labels;
                }
                else
                {
                    return "Deployment not found";
                }
            }
            // extract pod spec labels in the deployment
            labels = deploy.Spec.Template.Metadata.Labels;
            // get pods of this deployment by selecting labels
            var pods = await _client.CoreV1.ListNamespacedPodAsync(_namespace, labelSelector: $"{string.Join(",", labels.Select(label => $"{label.Key}={label.Value}"))}");
            var podNames = pods.Items.Select(pod => pod.Metadata.Name);
            return string.Join(", ", podNames);
        }

        // get spec and status of a deployment in a namespace
        public async Task<string> GetKubeDeploymentSpecStatusAsync(string resourceId, string _namespace, string deployment)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            // get deployment in namespace
            var deploy = await _client.AppsV1.ReadNamespacedDeploymentAsync(deployment, _namespace);
            if (deploy == null)
            {
                return "Deployment not found";
            }

            // Serialize to YAML
            return YamlHelper.Serialize(deploy);
        }

        // get spec and status of a Statefulset in a namespace
        public async Task<string> GetKubeStatefulsetSpecStatusAsync(string resourceId, string _namespace, string deployment)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            // get deployment in namespace
            var deploy = await _client.AppsV1.ReadNamespacedStatefulSetAsync(deployment, _namespace);
            if (deploy == null)
            {
                return "StatefulSet not found";
            }

            // Serialize to YAML
            return YamlHelper.Serialize(deploy);
        }

        // show events of a deployment in a namespace
        public async Task<string> GetKubeDeploymentEventsAsync(string resourceId, string _namespace, string deployment)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            // get deployment in namespace
            var deploy = await _client.AppsV1.ReadNamespacedDeploymentAsync(deployment, _namespace);
            if (deploy == null)
            {
                return "Deployment not found";
            }

            // get events of this deployment
            var events = await _client.CoreV1.ListNamespacedEventAsync(_namespace, fieldSelector: $"involvedObject.name={deployment},involvedObject.uid={deploy.Metadata.Uid}");
            var eventDescriptions = events.Items.Select(e => e.Message);
            return string.Join(", ", eventDescriptions);
        }

        // show events of a statefulset in a namespace
        public async Task<string> GetKubeStatefulSetEventsAsync(string resourceId, string _namespace, string deployment)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            // get deployment in namespace
            var deploy = await _client.AppsV1.ReadNamespacedStatefulSetAsync(deployment, _namespace);
            if (deploy == null)
            {
                return "Deployment not found";
            }

            // get events of this deployment
            var events = await _client.CoreV1.ListNamespacedEventAsync(_namespace, fieldSelector: $"involvedObject.name={deployment},involvedObject.uid={deploy.Metadata.Uid}");
            var eventDescriptions = events.Items.Select(e => e.Message);
            return string.Join(", ", eventDescriptions);
        }

        // rollout restart a deployment in a namespace
        public async Task<string> RolloutRestartDeploymentAsync(string resourceId, string _namespace, string deployment)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            // get deployment in namespace
            var deploy = await _client.AppsV1.ReadNamespacedDeploymentAsync(deployment, _namespace);
            if (deploy == null)
            {
                return "Deployment not found";
            }

            // patch the deployment to trigger a rollout restart
            var patch = new V1Patch("{\"spec\":{\"template\":{\"metadata\":{\"annotations\":{\"sreAgent/restartedAt\":\"" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) + "\"}}}}}", V1Patch.PatchType.MergePatch);
            await _client.AppsV1.PatchNamespacedDeploymentAsync(patch, deployment, _namespace);
            return "Deployment restarted";
        }

        // Scale a deployment in a namespace to the specified replica count
        public async Task<string> ScaleDeploymentAsync(string resourceId, string _namespace, string deployment, int replicaCount)
        {
            try
            {
                if (replicaCount < 0)
                {
                    return "Replica count must be a non-negative integer";
                }

                _client = await GetOrCreateClientAsync(resourceId);

                // Get deployment in namespace to verify it exists
                var deploy = await _client.AppsV1.ReadNamespacedDeploymentAsync(deployment, _namespace);
                if (deploy == null)
                {
                    return "Deployment not found";
                }

                // Log the current replica count before scaling
                _logger?.LogInformation(
                    "Scaling deployment {Deployment} in namespace {Namespace} from {CurrentReplicas} to {TargetReplicas} replicas",
                    deployment,
                    _namespace,
                    deploy.Spec.Replicas,
                    replicaCount);

                // Create patch to update the replica count
                var patch = new V1Patch(
                    $"{{\"spec\":{{\"replicas\":{replicaCount}}}}}",
                    V1Patch.PatchType.MergePatch);

                // Apply the patch to the deployment
                var patchResult = await _client.AppsV1.PatchNamespacedDeploymentAsync(
                    patch,
                    deployment,
                    _namespace);

                if (patchResult != null)
                {
                    string scaleDescription = replicaCount > deploy.Spec.Replicas
                        ? "scaled out"
                        : (replicaCount < deploy.Spec.Replicas ? "scaled in" : "replica count unchanged");

                    return $"Deployment {deployment} {scaleDescription} to {replicaCount} replicas";
                }

                return "Deployment scaling failed";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error scaling deployment {Deployment} in namespace {Namespace}", deployment, _namespace);
                return $"Error scaling deployment: {ex.Message}";
            }
        }

        // Scale a statefulset in a namespace to the specified replica count
        public async Task<string> ScaleStatefulSetAsync(string resourceId, string _namespace, string deployment, int replicaCount)
        {
            try
            {
                if (replicaCount < 0)
                {
                    return "Replica count must be a non-negative integer";
                }

                _client = await GetOrCreateClientAsync(resourceId);

                // Get deployment in namespace to verify it exists
                var deploy = await _client.AppsV1.ReadNamespacedStatefulSetAsync(deployment, _namespace);
                if (deploy == null)
                {
                    return "Deployment not found";
                }

                // Log the current replica count before scaling
                _logger?.LogInformation(
                    "Scaling statefulset {Deployment} in namespace {Namespace} from {CurrentReplicas} to {TargetReplicas} replicas",
                    deployment,
                    _namespace,
                    deploy.Spec.Replicas,
                    replicaCount);

                // Create patch to update the replica count
                var patch = new V1Patch(
                    $"{{\"spec\":{{\"replicas\":{replicaCount}}}}}",
                    V1Patch.PatchType.MergePatch);

                // Apply the patch to the deployment
                var patchResult = await _client.AppsV1.PatchNamespacedStatefulSetAsync(
                    patch,
                    deployment,
                    _namespace);

                if (patchResult != null)
                {
                    string scaleDescription = replicaCount > deploy.Spec.Replicas
                        ? "scaled out"
                        : (replicaCount < deploy.Spec.Replicas ? "scaled in" : "replica count unchanged");

                    return $"Deployment {deployment} {scaleDescription} to {replicaCount} replicas";
                }

                return "Deployment scaling failed";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error scaling Statefulset {Deployment} in namespace {Namespace}", deployment, _namespace);
                return $"Error scaling Statefulset: {ex.Message}";
            }
        }

        // show events of a pod in a namespace
        public async Task<string> GetKubePodEventsAsync(string resourceId, string _namespace, string pod)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            // get pod in namespace
            var podObj = await _client.CoreV1.ReadNamespacedPodAsync(pod, _namespace);
            if (podObj == null)
            {
                return "Pod not found";
            }

            // get events of this pod
            var events = await _client.CoreV1.ListNamespacedEventAsync(_namespace, fieldSelector: $"involvedObject.name={pod},involvedObject.uid={podObj.Metadata.Uid}");
            var eventDescriptions = events.Items.Select(e => e.Message);
            return string.Join(", ", eventDescriptions);
        }

        // show logs of a pod in a namespace with last several lines, default is 100
        public async Task<string> GetKubePodLogsAsync(string resourceId, string _namespace, string pod, int lines = 100)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            // get pod in namespace
            var podObj = await _client.CoreV1.ReadNamespacedPodAsync(pod, _namespace);
            if (podObj == null)
            {
                return "Pod not found";
            }

            // TODO: Determine container name based on pod spec
            string containerName = null;

            // get logs of this pod with HTTP messages
            var response = await _client.CoreV1.ReadNamespacedPodLogWithHttpMessagesAsync(
                pod,
                _namespace,
                container: containerName,  // Here's the container name needs to be specified
                tailLines: lines);

            if (response.Response.Content == null)
            {
                return string.Empty;
            }

            // read the stream to string
            using var reader = new StreamReader(await response.Response.Content.ReadAsStreamAsync());
            var rawLogs = await reader.ReadToEndAsync();
            var prompt = $"Summarize these logs from pod '{pod}' in namespace '{_namespace}'. IMPORTANT INSTRUCTIONS:\n" +
                         $"1. Preserve ALL error and warning messages with their complete context and timestamps\n" +
                         $"2. Maintain the full text of any exceptions, stack traces, or crash reports\n" +
                         $"3. Include startup/initialization messages and important state changes\n" +
                         $"4. For repetitive log patterns, show counts but include representative examples\n" +
                         $"5. Organize by log level (ERROR, WARNING, INFO) when possible\n" +
                         $"6. Keep exact text for any unusual or anomalous log entries\n" +
                         $"7. Preserve timing information for performance-related entries\n" +
                         $"---------------------------------------\n" +
                         $"Logs:\n" +
                         $"{rawLogs}";

            try
            {
                var chatResponse = await _chatClient.GetResponseAsync(prompt);
                return chatResponse.Messages.FirstOrDefault()?.Text ?? rawLogs;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error summarizing logs with chat service");
                return rawLogs; // Return raw logs if summarization fails
            }
        }

        // exec a command in a pod and get the output, container is optional, choose the first one if not specified
        public async Task<string> ExecCommandInPodAsync(string resourceId, string _namespace, string pod, string? container, string command)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            // get pod in namespace
            var podObj = await _client.CoreV1.ReadNamespacedPodAsync(pod, _namespace);
            if (podObj == null)
            {
                return "Pod not found";
            }
            if (string.IsNullOrEmpty(container))
            {
                container = podObj.Spec.Containers.FirstOrDefault()?.Name;
            }
            if (string.IsNullOrEmpty(container))
            {
                return "Container not found in pod " + pod;
            }

            var webSocket = await _client.WebSocketNamespacedPodExecAsync(
                pod,
                _namespace,
                command: ["sh", "-c", command],
                container: container);

            var memoryStream = new MemoryStream();
            var streamDemultiplexer = new StreamDemuxer(webSocket);
            streamDemultiplexer.Start();

            var stdoutStream = streamDemultiplexer.GetStream(1, 1);
            await stdoutStream.CopyToAsync(memoryStream);

            var output = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
            return output;
        }

        // list all CRD in cluster
        public async Task<string> ListCRDsAsync(string resourceId)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            var crds = await _client.ApiextensionsV1.ListCustomResourceDefinitionAsync();
            var crdInfo = crds.Items.Select(crd =>
            $"{crd.Metadata.Name} (Group: {crd.Spec.Group}, Kind: {crd.Spec.Names.Kind})"
            );

            return string.Join("\n", crdInfo);
        }

        public async Task<string> ListCustomResourcesAsync(string resourceId, string _namespace, string apiGroup, string kind)
        {
            try
            {
                _client = await GetOrCreateClientAsync(resourceId);
                // Get the plural name and version from CRDs
                var crds = await _client.ApiextensionsV1.ListCustomResourceDefinitionAsync();
                var crd = crds.Items.FirstOrDefault(c => c.Spec.Group == apiGroup &&
                    c.Spec.Names.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase));

                if (crd == null)
                {
                    return $"Custom Resource Definition for kind '{kind}' in group '{apiGroup}' not found";
                }

                string plural = crd.Spec.Names.Plural;
                string version = crd.Spec.Versions.FirstOrDefault(v => v.Served && v.Storage)?.Name
                    ?? crd.Spec.Versions.First().Name;

                // Get the custom resources
                var response = await _client.CustomObjects.ListNamespacedCustomObjectWithHttpMessagesAsync(
                    apiGroup, version, _namespace, plural);

                // Parse the response using System.Text.Json
                using var jsonDoc = JsonDocument.Parse(response.Body.ToString());
                var resourceNames = new List<string>();

                if (jsonDoc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        if (item.TryGetProperty("metadata", out var metadata) &&
                            metadata.TryGetProperty("name", out var name))
                        {
                            resourceNames.Add(name.GetString() ?? "unnamed");
                        }
                    }
                }

                if (resourceNames.Count == 0)
                {
                    return $"No {kind} resources found in namespace {_namespace}";
                }

                return string.Join(", ", resourceNames);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error listing custom resources");
                return $"Error listing custom resources: {ex.Message}";
            }
        }

        public async Task<string> GetCustomResourceYamlAsync(string resourceId, string _namespace, string apiGroup, string kind, string name)
        {
            try
            {
                _client = await GetOrCreateClientAsync(resourceId);
                // Get the plural name and version from CRDs
                var crds = await _client.ApiextensionsV1.ListCustomResourceDefinitionAsync();
                var crd = crds.Items.FirstOrDefault(c => c.Spec.Group == apiGroup &&
                    c.Spec.Names.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase));

                if (crd == null)
                {
                    return $"Custom Resource Definition for kind '{kind}' in group '{apiGroup}' not found";
                }

                string plural = crd.Spec.Names.Plural;
                string version = crd.Spec.Versions.FirstOrDefault(v => v.Served && v.Storage)?.Name
                    ?? crd.Spec.Versions.First().Name;

                // Get the custom resource
                var response = await _client.CustomObjects.GetNamespacedCustomObjectWithHttpMessagesAsync(
                    apiGroup, version, _namespace, plural, name);

                var expConverter = new ExpandoObjectConverter();
                dynamic deserializedObject = JsonConvert.DeserializeObject<ExpandoObject>(response.Body.ToString(), expConverter);

                var serializer = new Serializer();

                return serializer.Serialize(deserializedObject);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting custom resource YAML");
                return $"Error getting custom resource YAML: {ex.Message}";
            }
        }

        public async Task<string> GetPodYamlAsync(string resourceId, string _namespace, string pod)
        {
            try
            {
                _client = await GetOrCreateClientAsync(resourceId);
                var podObj = await _client.CoreV1.ReadNamespacedPodAsync(pod, _namespace);
                if (podObj == null)
                {
                    return "Pod not found";
                }

                return YamlHelper.Serialize(podObj);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting pod YAML");
                return $"Error getting pod YAML: {ex.Message}";
            }
        }

        public async Task<string> GetPodCpuMetricsForDeploymentAsync(string resourceId, string _namespace, string deployment, string timeRange = "5m")
        {
            return await GetInClusterPrometheusMetricsAsync(resourceId, _namespace, deployment, "cpu", timeRange);
        }

        public async Task<string> GetPodMemoryMetricsForDeploymentAsync(string resourceId, string _namespace, string deployment)
        {
            return await GetInClusterPrometheusMetricsAsync(resourceId, _namespace, deployment, "memory", "5m");
        }

        public async Task<string> GetSuccessRateMetricsAsync(string resourceId, string _namespace, string deployment, string timeRange = "5m")
        {
            return await GetInClusterPrometheusMetricsAsync(resourceId, _namespace, deployment, "success_rate", timeRange);
        }

        /// Fetches metrics from an in-cluster Prometheus instance.
        /// This method sets up port forwarding to the Prometheus service and queries it for the specified metrics.
        /// This is just for demo purpose, in production, we should fetch from Azure Monitor.
        public async Task<string> GetInClusterPrometheusMetricsAsync(string resourceId, string _namespace, string deployment, string metricType, string timeRange)
        {
            try
            {
                _client = await GetOrCreateClientAsync(resourceId);

                // TODO(jianbosun): make the prometheus service name, namespace configurable instead of hardcoded the discover way.
                var services = await _client.CoreV1.ListNamespacedServiceAsync(_namespace);
                var prometheusService = services.Items.FirstOrDefault(s => s.Metadata.Name.Contains("prometheus", StringComparison.OrdinalIgnoreCase));

                if (prometheusService == null)
                    return "Prometheus service not found in the cluster. Please ensure Prometheus is installed.";

                // Parse time range to Prometheus duration format
                string duration = ParseTimeRangeToDuration(timeRange);

                // Build the appropriate PromQL query based on metric type
                string query = BuildPromQuery(metricType, deployment, duration);

                _logger?.LogInformation("Using PromQL query: {Query}", query);

                var prometheusEndpoint = $"{prometheusService.Metadata.Name}:{prometheusService.Spec.Ports.First().Port}";
                var prometheusEndpointFromEnv = Environment.GetEnvironmentVariable("PrometheusEndpoint");
                if (!string.IsNullOrEmpty(prometheusEndpointFromEnv))
                {
                    prometheusEndpoint = prometheusEndpointFromEnv;
                }
                _logger?.LogInformation("Using Prometheus endpoint: {PrometheusEndpoint}", prometheusEndpoint);

                // Wait for port forwarding to establish
                await Task.Delay(1000);

                // Query Prometheus API
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                // Encode the query for URL
                string encodedQuery = Uri.EscapeDataString(query);
                string uri = $"http://{prometheusEndpoint}/api/v1/query?query={encodedQuery}";

                _logger?.LogInformation("Querying Prometheus API: {Uri}", uri);
                var response = await httpClient.GetAsync(uri);

                if (!response.IsSuccessStatusCode)
                    return $"Error querying Prometheus: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}";

                var content = await response.Content.ReadAsStringAsync();
                return FormatPrometheusResponse(content, metricType, deployment);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching Prometheus metrics for {MetricType} on deployment {Deployment} in namespace {Namespace}",
                    metricType, deployment, _namespace);
                return $"Error retrieving Prometheus metrics: {ex.Message}";
            }
        }

        // Get workloads that were updated within a specific time frame
        public async Task<string> GetRecentlyUpdatedWorkloadsAsync(string AKSClusterResourceId, string _namespace, int minutesAgo)
        {
            // 1. Validate Inputs
            if (minutesAgo <= 0)
            {
                _logger?.LogWarning("minutesAgo must be positive. Received {MinutesAgo} for namespace {Namespace}", minutesAgo, _namespace);
                return $"Invalid input: minutesAgo must be a positive number (received {minutesAgo}).";
            }
            if (string.IsNullOrWhiteSpace(AKSClusterResourceId))
            {
                _logger?.LogWarning("AKSClusterResourceId cannot be null or empty when checking namespace {Namespace}", _namespace);
                return "Invalid input: AKSClusterResourceId cannot be empty.";
            }
            if (string.IsNullOrWhiteSpace(_namespace))
            {
                _logger?.LogWarning("Namespace cannot be null or empty.");
                return "Invalid input: Namespace cannot be empty.";
            }


            var recentlyUpdated = new List<string>();
            var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo);

            try
            {
                // 2. Get Kubernetes Client
                IKubernetes client = await GetOrCreateClientAsync(AKSClusterResourceId); // Assuming GetOrCreateClientAsync exists

                // 3. Check Deployments in the specified namespace
                _logger?.LogInformation("Checking for Deployments updated since {CutoffTime} in namespace: {Namespace}",
                    cutoffTime.ToString("o"), _namespace);

                V1DeploymentList deploymentList;
                try
                {
                    deploymentList = await client.AppsV1.ListNamespacedDeploymentAsync(_namespace);
                }
                catch (Microsoft.Rest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger?.LogWarning("Namespace '{TargetNamespace}' not found when checking Deployments.", _namespace);
                    return $"Namespace '{_namespace}' not found."; // Namespace doesn't exist, return error
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error listing Deployments in namespace {Namespace}", _namespace);
                    return $"Error listing Deployments in namespace '{_namespace}': {ex.Message}";
                }


                foreach (var deployment in deploymentList.Items)
                {
                    if (string.Equals(deployment.Name(), "agent-web"))
                    {
                        _logger?.LogDebug("Skipping agent-web deployment");
                        continue; // Skip the agent-web deployment
                    }
                    // Check 'Progressing' condition first
                    var progressingCondition = deployment.Status?.Conditions?
                        .FirstOrDefault(c => c.Type == "Progressing");

                    if (progressingCondition?.LastUpdateTime != null && progressingCondition.LastUpdateTime >= cutoffTime)
                    {
                        string id = $"{deployment.Metadata.NamespaceProperty}/{deployment.Metadata.Name} (Deployment)";
                        recentlyUpdated.Add(id);
                        _logger?.LogDebug("Found recently updated Deployment: {WorkloadId}, LastUpdateTime: {UpdateTime}", id, progressingCondition.LastUpdateTime?.ToString("o"));
                    }
                    else // Fallback: Check latest time of *any* condition
                    {
                        var latestConditionTime = deployment.Status?.Conditions?
                            .Where(c => c.LastUpdateTime.HasValue)
                            .Max(c => (DateTimeOffset?)c.LastUpdateTime.Value);

                        if (latestConditionTime.HasValue && latestConditionTime >= cutoffTime)
                        {
                            string id = $"{deployment.Metadata.NamespaceProperty}/{deployment.Metadata.Name} (Deployment)";
                            if (!recentlyUpdated.Contains(id)) // Avoid adding duplicates
                            {
                                recentlyUpdated.Add(id);
                                _logger?.LogDebug("Found recently updated Deployment (based on latest condition): {WorkloadId}, LatestConditionTime: {UpdateTime}", id, latestConditionTime?.ToString("o"));
                            }
                        }
                    }
                }

                // 4. Check StatefulSets in the specified namespace
                _logger?.LogInformation("Checking for StatefulSets updated since {CutoffTime} in namespace: {Namespace}",
                    cutoffTime.ToString("o"), _namespace);

                V1StatefulSetList statefulSetList;
                try
                {
                    statefulSetList = await client.AppsV1.ListNamespacedStatefulSetAsync(_namespace);
                }
                catch (Microsoft.Rest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Namespace was likely already reported as not found from Deployment check, but log anyway.
                    _logger?.LogWarning("Namespace '{TargetNamespace}' not found when checking StatefulSets (already checked for Deployments).", _namespace);

                    statefulSetList = new V1StatefulSetList(items: new List<V1StatefulSet>()); // Assume empty list if somehow missed earlier
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error listing StatefulSets in namespace {Namespace}", _namespace);
                    return $"Error listing StatefulSets in namespace '{_namespace}': {ex.Message}";
                }


                foreach (var sts in statefulSetList.Items)
                {
                    bool generationMatch = (sts.Status?.ObservedGeneration ?? -1) >= (sts.Metadata?.Generation ?? 0);

                    // Use LastTransitionTime for StatefulSet Conditions
                    var latestConditionTime = sts.Status?.Conditions?
                        .Where(c => c.LastTransitionTime.HasValue)
                        .Max(c => (DateTimeOffset?)c.LastTransitionTime.Value);

                    if (latestConditionTime.HasValue && latestConditionTime >= cutoffTime)
                    {
                        string id = $"{sts.Metadata.NamespaceProperty}/{sts.Metadata.Name} (StatefulSet)";
                        recentlyUpdated.Add(id);
                        _logger?.LogDebug("Found recently updated StatefulSet: {WorkloadId}, LatestConditionTime: {UpdateTime}, GenerationMatch: {GenerationMatch}",
                             id, latestConditionTime?.ToString("o"), generationMatch);
                    }
                    else if (!generationMatch && sts.Metadata?.CreationTimestamp < DateTime.UtcNow.AddMinutes(-minutesAgo * 2)) // Check if spec changed recently (fallback)
                    {
                        var metadataUpdateTime = sts.Metadata?.ManagedFields?
                               .Where(mf => mf.Operation == "Update" && mf.Time.HasValue)
                               .Max(mf => (DateTimeOffset?)mf.Time.Value);

                        if (metadataUpdateTime.HasValue && metadataUpdateTime >= cutoffTime)
                        {
                            string id = $"{sts.Metadata.NamespaceProperty}/{sts.Metadata.Name} (StatefulSet)";
                            if (!recentlyUpdated.Contains(id)) // Avoid duplicates
                            {
                                recentlyUpdated.Add(id);
                                _logger?.LogDebug("Found potentially updated StatefulSet (generation mismatch): {WorkloadId}, Last Spec Update Time: {UpdateTime}", id, metadataUpdateTime?.ToString("o"));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error retrieving recently updated workloads for resourceId {ResourceId} and namespace {Namespace}", AKSClusterResourceId, _namespace);
                // Return an error message string
                return $"An error occurred while checking for recently updated workloads in namespace '{_namespace}': {ex.Message}";
            }

            // 5. Format the Result
            var distinctWorkloads = recentlyUpdated.Distinct().ToList();

            if (!distinctWorkloads.Any())
            {
                return $"No Deployments or StatefulSets found updated in the last {minutesAgo} minutes in namespace '{_namespace}'.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Workloads updated in the last {minutesAgo} minutes in namespace '{_namespace}':");
            foreach (var workload in distinctWorkloads)
            {
                sb.AppendLine($"- {workload}");
            }
            return sb.ToString().TrimEnd(); // Return the formatted string
        }

        // TODO(jianbosun): this is a hack for demo purpose, need to find a better way to discover prometheus query in a flexible way
        private string BuildPromQuery(string metricType, string deployment, string duration)
        {
            switch (metricType.ToLowerInvariant())
            {
                case "memory":
                    // Using existing pod-based memory query assuming deployment name maps to pod prefix
                    return $"sum(container_memory_usage_bytes{{pod=~\"{deployment}-.*\",container!=\"\"}}) by (pod) / sum(container_spec_memory_limit_bytes{{pod=~\"{deployment}-.*\",container!=\"\"}}) by (pod)";

                case "cpu":
                    // Using existing pod-based cpu query assuming deployment name maps to pod prefix
                    return $"sum(rate(container_cpu_usage_seconds_total{{pod=~\"{deployment}-.*\",container!=\"\"}}[{duration}])) by (pod) / sum(container_spec_cpu_quota{{pod=~\"{deployment}-.*\",container!=\"\"}} / 100000) by (pod)";

                case "success_rate":
                    // Check if the deployment is one of the specific ones requiring the rpc_server_requests metric
                    if (deployment.Equals("checkout", StringComparison.OrdinalIgnoreCase)) // Match exact service name from screenshot
                    {
                        string qparam = "oteldemo.CheckoutService";
                        _logger?.LogInformation("Building success rate query for supported service: {Deployment} using rpc_server_requests_per_rpc_count", deployment);
                        return $"sum(rate(rpc_server_requests_per_rpc_count{{rpc_service=\"{qparam}\", rpc_grpc_status_code=\"0\"}}[{duration}])) by (rpc_service) / sum(rate(rpc_server_requests_per_rpc_count{{rpc_service=\"{qparam}\"}}[{duration}])) by (rpc_service)";
                    }
                    else if (deployment.Equals("product-catalog"))
                    {
                        string qparam = "oteldemo.ProductCatalogService";
                        _logger?.LogInformation("Building success rate query for supported service: {Deployment} using rpc_server_requests_per_rpc_count", deployment);
                        return $"sum(rate(rpc_server_requests_per_rpc_count{{rpc_service=\"{qparam}\", rpc_grpc_status_code=\"0\"}}[{duration}])) by (rpc_service) / sum(rate(rpc_server_requests_per_rpc_count{{rpc_service=\"{qparam}\"}}[{duration}])) by (rpc_service)";
                    }
                    else
                    {
                        return $"Specific success rate query not configured for deployment '{deployment}'.";
                    }
                // Default case for custom queries or other unhandled metric types
                default:
                    return $"Specific query not configured for deployment '{deployment}'.";
            }
        }

        private string ParseTimeRangeToDuration(string timeRange)
        {
            // If timeRange is already in Prometheus format (e.g., "5m", "1h")
            if (Regex.IsMatch(timeRange, @"^\d+[smhdwy]$"))
                return timeRange;

            // Otherwise, try to interpret and convert
            if (timeRange.EndsWith("min"))
                return timeRange.Replace("min", "m");
            if (timeRange.EndsWith("minute") || timeRange.EndsWith("minutes"))
                return Regex.Replace(timeRange, @"minute[s]?$", "m");
            if (timeRange.EndsWith("hour") || timeRange.EndsWith("hours"))
                return Regex.Replace(timeRange, @"hour[s]?$", "h");
            if (timeRange.EndsWith("day") || timeRange.EndsWith("days"))
                return Regex.Replace(timeRange, @"day[s]?$", "d");

            // Default to 5m if parsing fails
            return "5m";
        }

        private string FormatPrometheusResponse(string prometheusJson, string metricType, string deployment)
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(prometheusJson);
                string status = jsonDoc.RootElement.GetProperty("status").GetString();

                if (status != "success")
                {
                    var errorType = jsonDoc.RootElement.GetProperty("errorType").GetString();
                    var error = jsonDoc.RootElement.GetProperty("error").GetString();
                    return $"Prometheus query error: {errorType} - {error}";
                }

                // Process results
                var data = jsonDoc.RootElement.GetProperty("data");
                var resultType = data.GetProperty("resultType").GetString();
                var results = data.GetProperty("result");

                if (results.GetArrayLength() == 0)
                    return $"No {metricType} metrics found for deployment {deployment}";

                var sb = new StringBuilder();
                sb.AppendLine($"## {metricType.ToUpperInvariant()} Metrics for {deployment}");
                sb.AppendLine();

                foreach (var result in results.EnumerateArray())
                {
                    // Get metric info (labels)
                    var metric = result.GetProperty("metric");
                    sb.Append("**Pod**: ");

                    // Try to get pod name if it exists
                    if (metric.TryGetProperty("pod", out var podElement))
                        sb.Append(podElement.GetString());
                    else if (metric.TryGetProperty("service_name", out var serviceElement))
                        sb.Append(serviceElement.GetString());
                    else
                        sb.Append("(unknown)");

                    sb.AppendLine();

                    // Process the value (depends on result type)
                    if (resultType == "vector")
                    {
                        var value = result.GetProperty("value");
                        var timestamp = value[0].GetDouble(); // Unix timestamp
                        var metricValue = value[1].GetString();

                        if (double.TryParse(metricValue, out double numericValue))
                        {
                            if (metricType.Equals("success_rate", StringComparison.OrdinalIgnoreCase))
                            {
                                sb.AppendLine($"**Success Rate**: {numericValue * 100:F2}%");
                            }
                            else if (metricType.Equals("memory", StringComparison.OrdinalIgnoreCase))
                            {
                                sb.AppendLine($"**Memory Usage**: {numericValue * 100:F2}% of limit");
                            }
                            else if (metricType.Equals("cpu", StringComparison.OrdinalIgnoreCase))
                            {
                                sb.AppendLine($"**CPU Usage**: {numericValue * 100:F2}% of limit");
                            }
                            else
                            {
                                sb.AppendLine($"**Value**: {numericValue}");
                            }
                        }
                        else
                        {
                            sb.AppendLine($"**Value**: {metricValue}");
                        }
                    }
                    else if (resultType == "matrix")
                    {
                        sb.AppendLine("**Time Series Values**:");
                        var values = result.GetProperty("values");

                        foreach (var pair in values.EnumerateArray().Take(5)) // Limit to first 5 values
                        {
                            var timestamp = pair[0].GetDouble(); // Unix timestamp
                            var dateTime = DateTimeOffset.FromUnixTimeSeconds((long)timestamp).ToString("yyyy-MM-dd HH:mm:ss");
                            var metricValue = pair[1].GetString();
                            sb.AppendLine($"- {dateTime}: {metricValue}");
                        }

                        if (values.GetArrayLength() > 5)
                            sb.AppendLine("*(showing first 5 values only)*");
                    }

                    sb.AppendLine();
                }

                // Add Grafana dashboard link based on metric type
                if (metricType.Equals("cpu", StringComparison.OrdinalIgnoreCase))
                {
                    string grafanaUrl = $"http://demo-agent.australiaeast.cloudapp.azure.com/grafana/d/W2gX2zHVk/demo-dashboard?orgId=1&from=now-30m&to=now&timezone=browser&var-service={deployment}&viewPanel=panel-6";
                    sb.AppendLine($"**Grafana Dashboard**: [View CPU metrics]({grafanaUrl})");
                    sb.AppendLine();
                }
                else if (metricType.Equals("memory", StringComparison.OrdinalIgnoreCase))
                {
                    string grafanaUrl = $"http://demo-agent.australiaeast.cloudapp.azure.com/grafana/d/W2gX2zHVk/demo-dashboard?orgId=1&from=now-30m&to=now&timezone=browser&var-service={deployment}&viewPanel=panel-8";
                    sb.AppendLine($"**Grafana Dashboard**: [View Memory metrics]({grafanaUrl})");
                    sb.AppendLine();
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error processing Prometheus response: {ex.Message}\nRaw response: {prometheusJson}";
            }
        }

    }
}