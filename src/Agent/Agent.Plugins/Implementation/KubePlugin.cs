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
using System.Collections.Concurrent;
using Azure.ResourceManager;
using Azure.Core;
using System.Text;
using System.Text.RegularExpressions;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;
using Agent.Prometheus.Services;
using Agent.Core.Configuration;
using Agent.Prometheus;
using Agent.Graph.Crawler.Metrics;
using Agent.Core.Services;
using Agent.Logging;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.ContainerService;

namespace Agent.Plugins
{
    public partial class KubePlugin : IKubePlugin
    {
        private readonly ILogger? _logger;
        private IKubernetes _client;
        private IChatClient _chatClient;
        private readonly ArmClient _armClient;
        private readonly IAuthenticationService _authService;
        private readonly IKubernetesClientFactory _kubernetesClientFactory;
        private readonly IPrometheusQueryService _prometheusQueryService;
        private readonly string? _prometheusQueryEndpoint;
        private readonly DashboardSettings _dashboardSettings;
        private readonly IAzureMetricsClient _azureMetricsClient;

        private ThreadContext Context { get; set; }
        private readonly ConcurrentDictionary<string, IKubernetes> _clientCache = new();
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(60);
        private readonly ConcurrentDictionary<string, DateTimeOffset> _cacheTimestamps = new();
        private const string AKSNodePoolLabel = "kubernetes.azure.com/agentpool";
        private const string LegacyAKSNodePoolLabel = "agentpool";

        public KubePlugin(
            IAuthenticationService authenticationService,
            IChatClient chatClient,
            IPrometheusQueryService prometheusQueryService,
            IAzureMetricsClient azureMetricsClient,
            DashboardSettings dashboardSettings,
            IKubernetesClientFactory kubernetesClientFactory,
            IArmClientFactory armClientFactory,
            ILogger<KubePlugin>? logger)
        {
            _logger = logger;
            _authService = authenticationService;
            _chatClient = chatClient;
            _prometheusQueryService = prometheusQueryService;
            _dashboardSettings = dashboardSettings;
            _azureMetricsClient = azureMetricsClient;
            _kubernetesClientFactory = kubernetesClientFactory;
            _armClient = armClientFactory.GetArmClient();

            _prometheusQueryEndpoint = _dashboardSettings.PrometheusUrl;
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

            var client = await _kubernetesClientFactory.CreateKubernetesClientFromResourceIdAsync(resourceId);
            if (client == null)
            {
                throw new InvalidOperationException($"Failed to create Kubernetes client for resourceId: {resourceId}");
            }
            // Cache the client
            _clientCache[resourceId] = client;
            _cacheTimestamps[resourceId] = DateTimeOffset.UtcNow;

            return client;
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

        // get all resource objects in a namespace with specific kind
        public async Task<string> ListKubeResourcesAsync(string resourceId, string _namespace, string kind)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            if (string.IsNullOrEmpty(kind))
            {
                return "Kind cannot be null or empty.";
            }
            IEnumerable<string> nameList;
            switch (kind.ToLowerInvariant())
            {
                case "deployment":
                case "deployments":
                    return await GetKubeDeploymentsAsync(resourceId, _namespace);
                case "service":
                case "services":
                    var services = await _client.CoreV1.ListNamespacedServiceAsync(_namespace);
                    nameList = services.Items.Select(service => service.Metadata.Name);
                    break;
                case "daemonset":
                case "daemonsets":
                    var ds = await _client.AppsV1.ListNamespacedDaemonSetAsync(_namespace);
                    nameList = ds.Items.Select(ds => ds.Metadata.Name);
                    break;
                case "statefulset":
                case "statefulsets":
                    var statefulSetList = await _client.AppsV1.ListNamespacedStatefulSetAsync(_namespace);
                    nameList = statefulSetList.Items.Select(sts => sts.Metadata.Name);
                    break;
                case "pod":
                case "pods":
                    var pods = await _client.CoreV1.ListNamespacedPodAsync(_namespace);
                    nameList = pods.Items.Select(pod => pod.Metadata.Name);
                    break;
                case "job":
                case "jobs":
                    var jobs = await _client.BatchV1.ListNamespacedJobAsync(_namespace);
                    nameList = jobs.Items.Select(job => job.Metadata.Name);
                    break;
                case "configmap":
                case "configmaps":
                    var configMaps = await _client.CoreV1.ListNamespacedConfigMapAsync(_namespace);
                    nameList = configMaps.Items.Select(cm => cm.Metadata.Name);
                    break;
                case "secret":
                case "secrets":
                    var secrets = await _client.CoreV1.ListNamespacedSecretAsync(_namespace);
                    nameList = secrets.Items.Select(secret => secret.Metadata.Name);
                    break;
                case "ingress":
                case "ingresses":
                    var ingresses = await _client.NetworkingV1.ListNamespacedIngressAsync(_namespace);
                    nameList = ingresses.Items.Select(ingress => ingress.Metadata.Name);
                    break;
                case "replicaset":
                case "replicasets":
                    var replicaSets = await _client.AppsV1.ListNamespacedReplicaSetAsync(_namespace);
                    nameList = replicaSets.Items.Select(rs => rs.Metadata.Name);
                    break;
                default:
                    return $"Unsupported kind: {kind}.";
            }

            return string.Join(", ", nameList);
        }

        public async Task<string> DiagnoseAKSAppAsync(string resourceId, string _namespace, string kind, string name)
        {
            _logger?.LogInternalInformation("Diagnosing {Kind} {Name} in namespace {Namespace}", kind, name, _namespace);
            var diagnosis = new StringBuilder();
            var tasks = new List<Task>();

            // Dictionary to store task results
            var results = new Dictionary<string, string>();

            switch (kind.ToLowerInvariant())
            {
                case "deployment":
                    tasks.Add(GetKubeDeploymentSpecStatusAsync(resourceId, _namespace, name)
                        .ContinueWith(task => results["SpecStatus"] = task.Result));

                    tasks.Add(GetKubeDeploymentEventsAsync(resourceId, _namespace, name)
                        .ContinueWith(task => results["Events"] = task.Result));
                    break;

                case "statefulset":
                    tasks.Add(GetKubeStatefulsetSpecStatusAsync(resourceId, _namespace, name)
                        .ContinueWith(task => results["SpecStatus"] = task.Result));

                    tasks.Add(GetKubeStatefulSetEventsAsync(resourceId, _namespace, name)
                        .ContinueWith(task => results["Events"] = task.Result));
                    break;

                default:
                    return "Unsupported kind. Only Deployment and StatefulSet are supported.";
            }

            // Get the start and end times for the last 30 minutes metrics
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddMinutes(-30);

            tasks.Add(GetKubeResourceMetricsRangeAsync(resourceId, _namespace, kind, name, "cpu", startTime.ToString("o"), endTime.ToString("o"))
            .ContinueWith(task => results["CpuMetrics"] = task.Result));

            tasks.Add(GetKubeResourceMetricsRangeAsync(resourceId, _namespace, kind, name, "memory", startTime.ToString("o"), endTime.ToString("o"))
                .ContinueWith(task => results["MemoryMetrics"] = task.Result));

            tasks.Add(GetKubeResourceMetricsRangeAsync(resourceId, _namespace, kind, name, "availability", startTime.ToString("o"), endTime.ToString("o"))
                .ContinueWith(task => results["AvailabilityMetrics"] = task.Result));

            // Get pods and their diagnostics information in parallel
            var podListTask = GetKubePodsAsync(resourceId, _namespace, kind, name);
            tasks.Add(podListTask);

            await Task.WhenAll(tasks);

            // Process pod diagnostics after we have the pod list
            var podList = podListTask.Result;
            var podTasks = new List<Task>();
            var podResults = new Dictionary<string, Dictionary<string, string>>();

            foreach (var pod in podList.Split(", ").Where(p => !string.IsNullOrEmpty(p)))
            {
                _logger.LogInternalInformation("Diagnosing pod: {Pod} for component: {Name}", pod, name);
                podResults[pod] = new Dictionary<string, string>();
                podTasks.Add(GetKubePodSpecStatusAsync(resourceId, _namespace, pod)
                    .ContinueWith(task => podResults[pod]["PodYaml"] = task.Result));

                podTasks.Add(GetKubeResourceEventsAsync(resourceId, _namespace, "", "Pod", pod)
                    .ContinueWith(task => podResults[pod]["Events"] = task.Result));

                podTasks.Add(GetKubePodLogsAsync(resourceId, _namespace, pod)
                    .ContinueWith(task => podResults[pod]["Logs"] = task.Result));
            }

            await Task.WhenAll(podTasks);

            // Build the final diagnosis output
            diagnosis.AppendLine($"{kind} Spec and Status:");
            diagnosis.AppendLine(results["SpecStatus"]);
            diagnosis.AppendLine($"{kind} Events:");
            diagnosis.AppendLine(results["Events"]);
            diagnosis.AppendLine("CPU Metrics:");
            diagnosis.AppendLine(results["CpuMetrics"]);
            diagnosis.AppendLine("Memory Metrics:");
            diagnosis.AppendLine(results["MemoryMetrics"]);
            diagnosis.AppendLine("Availability Metrics:");
            diagnosis.AppendLine(results["AvailabilityMetrics"]);

            // Add pod diagnostics
            foreach (var pod in podResults.Keys)
            {
                diagnosis.AppendLine($"Pod Spec and Status for {pod}:");
                diagnosis.AppendLine(podResults[pod]["PodYaml"]);
                diagnosis.AppendLine($"Pod Events for {pod}:");
                diagnosis.AppendLine(podResults[pod]["Events"]);
                diagnosis.AppendLine($"Pod Logs for {pod}:");
                diagnosis.AppendLine(podResults[pod]["Logs"]);
            }

            return diagnosis.ToString();
        }

        public async Task<string> GetKubePodsAsync(string resourceId, string _namespace, string kind, string name)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            IDictionary<string, string> labels = new Dictionary<string, string>();
            switch (kind.ToLowerInvariant())
            {
                case "deployment":
                    // get deployment in namespace
                    var deploy = await _client.AppsV1.ReadNamespacedDeploymentAsync(name, _namespace);
                    // extract pod spec labels in the deployment
                    labels = deploy.Spec.Template.Metadata.Labels;
                    break;
                case "statefulset":
                    // Fallback to redis deployment if specified
                    var sts = await _client.AppsV1.ReadNamespacedStatefulSetAsync(name, _namespace);
                    labels = sts.Spec.Template.Metadata.Labels;
                    break;
                default:
                    return "Unsupported kind. Only Deployment and StatefulSet are supported.";
            }
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

        // get spec and status of a replicaset in a namespace
        public async Task<string> GetKubeReplicasetSpecStatusAsync(string resourceId, string _namespace, string replicaset)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            // get replicaset in namespace
            var rs = await _client.AppsV1.ReadNamespacedReplicaSetAsync(replicaset, _namespace);
            if (rs == null)
            {
                return "ReplicaSet not found";
            }

            // Serialize to YAML
            return YamlHelper.Serialize(rs);
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

        // get spec and status of a DaemonSet in a namespace
        public async Task<string> GetKubeDaemonsetSpecStatusAsync(string resourceId, string _namespace, string daemonset)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            // get daemonset in namespace
            var ds = await _client.AppsV1.ReadNamespacedDaemonSetAsync(daemonset, _namespace);
            if (ds == null)
            {
                return "DaemonSet not found";
            }

            // Serialize to YAML
            return YamlHelper.Serialize(ds);
        }

        // get spec and status of a Pod in a namespace
        public async Task<string> GetKubePodSpecStatusAsync(string resourceId, string _namespace, string pod)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            // get pod in namespace
            var podObj = await _client.CoreV1.ReadNamespacedPodAsync(pod, _namespace);
            if (podObj == null)
            {
                return "Pod not found";
            }

            // Serialize to YAML
            return YamlHelper.Serialize(podObj);
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
                _logger?.LogInternalInformation(
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
                _logger?.LogInternalError(ex, "Error scaling deployment {Deployment} in namespace {Namespace}", deployment, _namespace);
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
                _logger?.LogInternalInformation(
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
                _logger?.LogInternalError(ex, "Error scaling Statefulset {Deployment} in namespace {Namespace}", deployment, _namespace);
                return $"Error scaling Statefulset: {ex.Message}";
            }
        }



        // show logs of a pod in a namespace with last several lines, default is 100
        public async Task<string> GetKubePodLogsAsync(string resourceId, string _namespace, string pod, string containerName = "", int lines = 100)
        {

            _client = await GetOrCreateClientAsync(resourceId);
            // get pod in namespace
            var podObj = await _client.CoreV1.ReadNamespacedPodAsync(pod, _namespace);
            if (podObj == null)
            {
                return "Pod not found";
            }

            if (string.IsNullOrEmpty(containerName))
            {
                containerName = podObj.Spec.Containers.FirstOrDefault()?.Name;
            }

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
                var chatResponse = await ChatClientHelper.ExecuteWithRetryAsync(
                    async () => await _chatClient.GetResponseAsync(prompt, new ChatOptions
                    {
                        Temperature = 0.5f,
                    }),
                    _logger, 10);
                return chatResponse.Messages.FirstOrDefault()?.Text ?? rawLogs;
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Error summarizing logs with chat service");
                return rawLogs; // Return raw logs if summarization fails
            }
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
                _logger?.LogInternalError(ex, "Error listing custom resources");
                return $"Error listing custom resources: {ex.Message}";
            }
        }


        private static readonly TimeSpan[] SupportedBuckets = new[]
        {
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(15),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(6),
            TimeSpan.FromHours(12),
            TimeSpan.FromDays(1)
        };

        public static TimeSpan CalculateGranularity(DateTime startTime, DateTime endTime)
        {
            var duration = endTime - startTime;

            // Calculate minimum granularity to keep results under 1440 points
            var minGranularity = TimeSpan.FromTicks(duration.Ticks / 1440);

            // Pick the first supported bucket >= minGranularity
            var matchingBucket = SupportedBuckets.FirstOrDefault(bucket => bucket >= minGranularity);

            // If no bucket is large enough, use the largest bucket (1 day)
            return matchingBucket != default ? matchingBucket : SupportedBuckets.Last();
        }
        public async Task<string> GetKubeResourceMetricsRangeAsync(string AKSClusterResourceId, string _namespace, string kind, string name, string metricsType, string startTime, string endTime)
        {
            // Build the PromQL queries based on the specified metric type
            string[] queries = BuildPromQueries(metricsType, _namespace, kind, name, "");
            DateTime startDate = ParseDateTime(startTime);
            DateTime endDate = ParseDateTime(endTime);
            var step = CalculateGranularity(startDate, endDate);

            string? lastError = null;
            foreach (var query in queries)
            {
                if (string.IsNullOrEmpty(query) || query.StartsWith("No query", StringComparison.OrdinalIgnoreCase))
                {
                    lastError = $"Failed to build a valid PromQL query for metric type '{metricsType}' in namespace '{_namespace}', workload type '{kind}', and workload name '{name}'";
                    _logger?.LogInternalWarning(lastError, metricsType, _namespace, kind, name);
                    continue;
                }

                _logger?.LogInternalInformation(
                    "Executing PromQL against Azure Monitor Prometheus endpoint '{Endpoint}': {Query}",
                    _prometheusQueryEndpoint, query);

                // Query the Prometheus endpoint using the injected service
                var response = await _prometheusQueryService.QueryRangeAsync(_prometheusQueryEndpoint, query, startDate, endDate, step);

                // Check if response has metric data
                if (response is SuccessMatrixResponse successMatrix && successMatrix.Data != null && successMatrix.Data.Result != null && successMatrix.Data.Result.Any())
                {
                    return FormatPrometheusRangeResponse(response, metricsType, kind, name, startTime, endTime);
                }
                else if (response is ErrorResponse errorResponse)
                {
                    lastError = $"Error from Prometheus: {errorResponse.ErrorType} - {errorResponse.Error}";
                }
            }
            // If no query returned data, return the last error or a generic message
            return lastError ?? $"No {metricsType} metrics found for workloadType {kind} and workloadName {name}. Check if the values specified are correct and if metrics are being collected.";
        }

        // Helper method to parse date strings into DateTime objects
        private DateTime ParseDateTime(string timeDate)
        {
            // Try to parse the time as an absolute date/time
            if (DateTime.TryParse(timeDate, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsedTime))
            {
                return parsedTime;
            }

            // If it's a relative time like "1h ago", "30m ago", etc.
            if (timeDate.Contains("ago", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(timeDate, @"(\d+)\s*([smhdwy]|min|hour|day|week|month|year)s?\s*ago",
                    RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    int value = int.Parse(match.Groups[1].Value);
                    string unit = match.Groups[2].Value.ToLowerInvariant();

                    return unit switch
                    {
                        "s" => DateTime.UtcNow.AddSeconds(-value),
                        "m" or "min" => DateTime.UtcNow.AddMinutes(-value),
                        "h" or "hour" => DateTime.UtcNow.AddHours(-value),
                        "d" or "day" => DateTime.UtcNow.AddDays(-value),
                        "w" or "week" => DateTime.UtcNow.AddDays(-value * 7),
                        "y" or "year" => DateTime.UtcNow.AddYears(-value),
                        _ => DateTime.UtcNow.AddMinutes(-value) // Default to minutes
                    };
                }
            }

            // Default to current time if parsing fails
            _logger?.LogInternalWarning("Failed to parse time string: {TimeDate}. Using current time.", timeDate);
            return DateTime.UtcNow;
        }

        // Helper method to convert duration strings to TimeSpan
        private TimeSpan ParseTimeSpan(string duration)
        {
            var match = Regex.Match(duration, @"^(\d+)([smhdwy])$");
            if (match.Success)
            {
                int value = int.Parse(match.Groups[1].Value);
                string unit = match.Groups[2].Value;

                return unit switch
                {
                    "s" => TimeSpan.FromSeconds(value),
                    "m" => TimeSpan.FromMinutes(value),
                    "h" => TimeSpan.FromHours(value),
                    "d" => TimeSpan.FromDays(value),
                    "w" => TimeSpan.FromDays(value * 7),
                    "y" => TimeSpan.FromDays(value * 365), // Approximation
                    _ => TimeSpan.FromMinutes(1) // Default to 1 minute
                };
            }

            // Default to 15 seconds if parsing fails
            return TimeSpan.FromSeconds(15);
        }

        public async Task<string> GetKubeResourceEventsAsync(string resourceId, string _namespace, string apiGroup, string kind, string name)
        {
            try
            {
                _client = await GetOrCreateClientAsync(resourceId);
                string uid;

                switch (kind.ToLowerInvariant())
                {
                    case "deployment":
                        var deployment = await _client.AppsV1.ReadNamespacedDeploymentAsync(name, _namespace);
                        uid = deployment.Metadata.Uid;
                        break;
                    case "statefulset":
                        var statefulset = await _client.AppsV1.ReadNamespacedStatefulSetAsync(name, _namespace);
                        uid = statefulset.Metadata.Uid;
                        break;
                    case "daemonset":
                        var daemonset = await _client.AppsV1.ReadNamespacedDaemonSetAsync(name, _namespace);
                        uid = daemonset.Metadata.Uid;
                        break;
                    case "service":
                        var service = await _client.CoreV1.ReadNamespacedServiceAsync(name, _namespace);
                        uid = service.Metadata.Uid;
                        break;
                    case "pod":
                        var pod = await _client.CoreV1.ReadNamespacedPodAsync(name, _namespace);
                        uid = pod.Metadata.Uid;
                        break;
                    default:
                        // Handle custom resources
                        try
                        {
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
                            var response = await _client.CustomObjects.GetNamespacedCustomObjectAsync(
                                apiGroup, version, _namespace, plural, name);

                            // Extract UID from JSON response
                            if (response is IDictionary<string, object> resource &&
                                resource.TryGetValue("metadata", out var metadataObj) &&
                                metadataObj is IDictionary<string, object> metadata &&
                                metadata.TryGetValue("uid", out var uidObj))
                            {
                                uid = uidObj.ToString();
                            }
                            else
                            {
                                return $"Could not extract UID from custom resource {kind}/{name}";
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogInternalError(ex, $"Error getting custom resource {kind}/{name} in namespace {_namespace}");
                            return $"Error getting custom resource events: {ex.Message}";
                        }
                        break;
                }

                // Get events for this resource
                var events = await _client.CoreV1.ListNamespacedEventAsync(_namespace,
                    fieldSelector: $"involvedObject.name={name},involvedObject.uid={uid}");

                if (events.Items.Count == 0)
                {
                    return $"No events found for {kind} '{name}' in namespace {_namespace}";
                }

                // Format events with timestamp, type and message
                var formattedEvents = events.Items.Select(e =>
                    $"[{e.LastTimestamp:yyyy-MM-dd HH:mm:ss}] {e.Type}: {e.Message}");

                return string.Join("\n", formattedEvents);
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, $"Error getting events for {kind}, {name}");
                return $"Error getting events for {kind}, {name}: {ex.Message}";
            }
        }

        public async Task<string> GetKubeResourceSpecStatusAsync(string resourceId, string _namespace, string apiGroup, string kind, string name)
        {
            try
            {
                switch (kind.ToLowerInvariant())
                {
                    case "deployment":
                        return await GetKubeDeploymentSpecStatusAsync(resourceId, _namespace, name);
                    case "replicaset":
                        return await GetKubeReplicasetSpecStatusAsync(resourceId, _namespace, name);
                    case "statefulset":
                        return await GetKubeStatefulsetSpecStatusAsync(resourceId, _namespace, name);
                    case "daemonset":
                        return await GetKubeDaemonsetSpecStatusAsync(resourceId, _namespace, name);
                    case "pod":
                        return await GetKubePodSpecStatusAsync(resourceId, _namespace, name);
                    case "service":
                        _client = await GetOrCreateClientAsync(resourceId);
                        var service = await _client.CoreV1.ReadNamespacedServiceAsync(name, _namespace);
                        if (service == null)
                        {
                            return "Service not found";
                        }
                        return YamlHelper.Serialize(service);
                    case "configmap":
                        _client = await GetOrCreateClientAsync(resourceId);
                        var configMap = await _client.CoreV1.ReadNamespacedConfigMapAsync(name, _namespace);
                        if (configMap == null)
                        {
                            return "ConfigMap not found";
                        }
                        return YamlHelper.Serialize(configMap);
                    case "persistentvolume":
                    case "pv":
                        _client = await GetOrCreateClientAsync(resourceId);
                        var pv = await _client.CoreV1.ReadPersistentVolumeAsync(name);
                        if (pv == null)
                        {
                            return "PV not found";
                        }
                        return YamlHelper.Serialize(pv);
                    case "persistentvolumeclaim":
                    case "pvc":
                        _client = await GetOrCreateClientAsync(resourceId);
                        var pvc = await _client.CoreV1.ReadNamespacedPersistentVolumeClaimAsync(name, _namespace);
                        if (pvc == null)
                        {
                            return "PVC not found";
                        }
                        return YamlHelper.Serialize(pvc);
                    case "ingress":
                        _client = await GetOrCreateClientAsync(resourceId);
                        var ingress = await _client.NetworkingV1.ReadNamespacedIngressAsync(name, _namespace);
                        if (ingress == null)
                        {
                            return "Ingress not found";
                        }
                        return YamlHelper.Serialize(ingress);
                    case "cronjob":
                        _client = await GetOrCreateClientAsync(resourceId);
                        var cronJob = await _client.BatchV1.ReadNamespacedCronJobAsync(name, _namespace);
                        if (cronJob == null)
                        {
                            return "CronJob not found";
                        }
                        return YamlHelper.Serialize(cronJob);
                    case "job":
                        _client = await GetOrCreateClientAsync(resourceId);
                        var job = await _client.BatchV1.ReadNamespacedJobAsync(name, _namespace);
                        if (job == null)
                        {
                            return "Job not found";
                        }
                        return YamlHelper.Serialize(job);

                }

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
                var response = await _client.CustomObjects.GetNamespacedCustomObjectAsync(
                    apiGroup, version, _namespace, plural, name);

                return YamlHelper.Serialize(response);
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, $"Error getting spec status for {kind}, {name} ");
                return $"Error getting custom resource YAML: {ex.Message}";
            }
        }

        public async Task<string> GetCpuMetricsForWorkloadAsync(string AKSClusterResourceId, string _namespace, string workloadType, string workloadName, string timeRange = "5m")
        {
            return await GetAzureMonitorPrometheusMetricsAsync(AKSClusterResourceId, _namespace, workloadType, workloadName, "cpu", timeRange);
        }

        public async Task<string> GetMemoryMetricsForWorkloadAsync(string AKSClusterResourceId, string _namespace, string workloadType, string workloadName, string timeRange = "5m")
        {
            return await GetAzureMonitorPrometheusMetricsAsync(AKSClusterResourceId, _namespace, workloadType, workloadName, "memory", timeRange);
        }

        /// <summary>
        /// Fetches metrics from Azure Monitor Prometheus endpoint specified by the user.
        /// </summary>
        private async Task<string> GetAzureMonitorPrometheusMetricsAsync(
            string resourceId,
            string _namespace,
            string workloadType,
            string workloadName,
            string metricType,
            string timeRange)
        {
            if (string.IsNullOrEmpty(_prometheusQueryEndpoint))
            {
                return "Azure Monitor Prometheus query endpoint is not configured in the agent settings.";
            }

            try
            {
                // Convert the provided time range into Prometheus-compatible duration format
                string duration = ParseTimeRangeToDuration(timeRange);

                // Build the PromQL query based on the specified metric type
                string[] queries = BuildPromQueries(metricType, _namespace, workloadType, workloadName, duration);
                string query = queries.FirstOrDefault() ?? string.Empty;

                if (string.IsNullOrEmpty(query) || query.StartsWith("No query", StringComparison.OrdinalIgnoreCase))
                {

                    var errorResponse = $"Failed to build a valid PromQL query for metric type '{metricType}' in namespace '{_namespace}', workload type '{workloadType}', and workload name '{workloadName}'";
                    _logger?.LogInternalWarning(
                        errorResponse,
                        metricType, _namespace, workloadType, workloadName);
                    return errorResponse;
                }

                _logger?.LogInternalInformation(
                    "Executing PromQL against Azure Monitor Prometheus endpoint '{Endpoint}': {Query}",
                    _prometheusQueryEndpoint, query);

                // Query the Prometheus endpoint using the injected service
                var response = await _prometheusQueryService.QueryInstantAsync(_prometheusQueryEndpoint, query);

                return FormatPrometheusResponse(response, metricType, workloadType, workloadName);
            }
            catch (HttpRequestException httpEx)
            {
                _logger?.LogInternalError(
                    httpEx,
                    "HTTP error while querying Azure Monitor Prometheus for metric type '{MetricType}' in namespace '{Namespace}', workload type '{WorkloadType}', and workload name '{WorkloadName}'.",
                    metricType, _namespace, workloadType, workloadName);
                return $"HTTP error while querying Azure Monitor Prometheus: {httpEx.Message} (StatusCode: {httpEx.StatusCode})";
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(
                    ex,
                    "Unexpected error while fetching Prometheus metrics for metric type '{MetricType}' in namespace '{Namespace}', workload type '{WorkloadType}', and workload name '{WorkloadName}'.",
                    metricType, _namespace, workloadType, workloadName);
                return $"Unexpected error retrieving Prometheus metrics: {ex.Message}";
            }
        }


        private static string FormatPrometheusResponse(Response? response, string metricType, string workloadType, string workloadName)
        {
            if (response == null)
            {
                return $"No response received from Prometheus for {metricType} metrics for workloadType {workloadType} and workloadName {workloadName}.";
            }

            var sb = new StringBuilder();
            string capitalizedMetricType = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(metricType.ToLowerInvariant()); // e.g., "Cpu", "Memory"

            switch (response)
            {
                case ErrorResponse errorResponse:
                    sb.AppendLine($"## Error Fetching {capitalizedMetricType} Metrics for workloadType {workloadType} and workloadName {workloadName}.");
                    sb.AppendLine();
                    sb.AppendLine($"**Error Type**: {errorResponse.ErrorType}");
                    sb.AppendLine($"**Error Message**: {errorResponse.Error}");
                    if (errorResponse.Warnings?.Any() ?? false)
                    {
                        sb.AppendLine("**Warnings**:");
                        foreach (var warning in errorResponse.Warnings)
                        {
                            sb.AppendLine($"- {warning}");
                        }
                    }
                    break;

                case SuccessVectorResponse successVector:
                    var vectorData = successVector.Data;
                    if (vectorData?.Result == null || !vectorData.Result.Any())
                    {
                        return $"No {metricType} metrics found for workloadType {workloadType} and workloadName {workloadName}.'. Check if the values specified are correct and if metrics are being collected.";
                    }

                    sb.AppendLine($"## {capitalizedMetricType} Usage for workloadType {workloadType} and workloadName {workloadName}.");
                    sb.AppendLine();

                    foreach (var resultItem in vectorData.Result)
                    {
                        // --- Pod Name ---
                        string podName = "(unknown pod)";
                        if (resultItem.Metric.TryGetValue("pod", out var podLabel))
                        {
                            podName = podLabel;
                        }
                        else if (resultItem.Metric.TryGetValue("kubernetes_pod_name", out var k8sPodLabel)) // Alternative label
                        {
                            podName = k8sPodLabel;
                        }
                        else if (resultItem.Metric.TryGetValue("name", out var nameLabel)) // Another possible label
                        {
                            podName = nameLabel;
                        }
                        // You might need to add more fallbacks depending on your exact metric labels

                        sb.Append($"**Pod**: `{podName}`");

                        // --- Container Name (if available) ---
                        if (resultItem.Metric.TryGetValue("container", out var containerLabel) && !string.IsNullOrEmpty(containerLabel))
                        {
                            sb.Append($" (Container: `{containerLabel}`)");
                        }
                        sb.AppendLine();

                        // --- Metric Value ---
                        double timestamp = resultItem.Value.Item1; // Unix timestamp (seconds)
                        string rawValue = resultItem.Value.Item2;
                        DateTimeOffset dateTime = DateTimeOffset.FromUnixTimeSeconds((long)timestamp);

                        if (double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out double numericValue))
                        {
                            // Check for NaN or Infinity which Prometheus can return
                            if (double.IsNaN(numericValue) || double.IsInfinity(numericValue))
                            {
                                sb.AppendLine($"**{capitalizedMetricType} Value**: {rawValue} (at {dateTime:yyyy-MM-dd HH:mm:ss zz})");
                            }
                            // Specific formatting for CPU/Memory (assuming they represent % usage from your queries)
                            else if (metricType.Equals("cpu", StringComparison.OrdinalIgnoreCase) || metricType.Equals("memory", StringComparison.OrdinalIgnoreCase))
                            {
                                // Your queries calculate percentage, so multiply by 100
                                sb.AppendLine($"**{capitalizedMetricType} Usage**: {numericValue:F2}% of limit (at {dateTime:yyyy-MM-dd HH:mm:ss zz})");
                            }
                            else // Generic numeric value
                            {
                                sb.AppendLine($"**Value**: {numericValue:F4} (at {dateTime:yyyy-MM-dd HH:mm:ss zz})");
                            }
                        }
                        else // Value wasn't a parsable number
                        {
                            sb.AppendLine($"**{capitalizedMetricType} Value**: {rawValue} (at {dateTime:yyyy-MM-dd HH:mm:ss zz})");
                        }
                        sb.AppendLine(); // Add a blank line between pod results
                    }

                    break;
                default:
                    // Handle unknown response types if necessary, although your models cover the main Prometheus ones.
                    sb.AppendLine($"## Unknown Prometheus Response Type for {metricType} Metrics for workloadType {workloadType} and workloadName {workloadName}.");
                    sb.AppendLine($"Received type: {response.GetType().Name}");
                    break;
            }

            return sb.ToString().TrimEnd(); // Trim trailing whitespace/newlines
        }

        private static string FormatPrometheusRangeResponse(Response? response, string metricType, string workloadType, string workloadName, string startTime, string endTime)
        {
            if (response == null)
            {
                return $"No response received from Prometheus for {metricType} metrics for workloadType {workloadType} and workloadName {workloadName}.";
            }

            var sb = new StringBuilder();
            string capitalizedMetricType = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(metricType.ToLowerInvariant()); // e.g., "Cpu", "Memory"

            switch (response)
            {
                case ErrorResponse errorResponse:
                    sb.AppendLine($"## Error Fetching {capitalizedMetricType} Metrics for workloadType {workloadType} and workloadName {workloadName}.");
                    sb.AppendLine();
                    sb.AppendLine($"**Error Type**: {errorResponse.ErrorType}");
                    sb.AppendLine($"**Error Message**: {errorResponse.Error}");
                    if (errorResponse.Warnings?.Any() ?? false)
                    {
                        sb.AppendLine("**Warnings**:");
                        foreach (var warning in errorResponse.Warnings)
                        {
                            sb.AppendLine($"- {warning}");
                        }
                    }
                    break;

                case SuccessMatrixResponse successMatrix:
                    var matrixData = successMatrix.Data;
                    if (matrixData?.Result == null || !matrixData.Result.Any())
                    {
                        return $"No {metricType} metrics found for workloadType {workloadType} and workloadName {workloadName}.'. Check if the values specified are correct and if metrics are being collected.";
                    }

                    var dataPoints = new List<string>();

                    foreach (var resultItem in matrixData.Result)
                    {
                        // Get pod name for reference (optional, not used in final output)
                        string podName = "(unknown pod)";
                        if (resultItem.Metric.TryGetValue("pod", out var podLabel))
                        {
                            podName = podLabel;
                        }
                        else if (resultItem.Metric.TryGetValue("kubernetes_pod_name", out var k8sPodLabel))
                        {
                            podName = k8sPodLabel;
                        }
                        else if (resultItem.Metric.TryGetValue("name", out var nameLabel))
                        {
                            podName = nameLabel;
                        }

                        foreach (var metricItem in resultItem.Values)
                        {
                            double timestamp = metricItem.Item1; // Unix timestamp (seconds)
                            string rawValue = metricItem.Item2;
                            DateTimeOffset dateTime = DateTimeOffset.FromUnixTimeSeconds((long)timestamp);

                            if (double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out double numericValue))
                            {
                                // Format: timestamp|value|metricType
                                var dataPoint = $"{dateTime:yyyy-MM-ddTHH:mm:ss}|{numericValue:F2}|{capitalizedMetricType} Usage";
                                dataPoints.Add(dataPoint);
                            }
                        }
                    }

                    // Join all data points with semicolons
                    return string.Join(";", dataPoints);

                    break;
                default:
                    // Handle unknown response types if necessary, although your models cover the main Prometheus ones.
                    sb.AppendLine($"## Unknown Prometheus Response Type for {metricType} Metrics for workloadType {workloadType} and workloadName {workloadName}.");
                    sb.AppendLine($"Received type: {response.GetType().Name}");
                    break;
            }

            return sb.ToString().TrimEnd(); // Trim trailing whitespace/newlines
        }

        // Get workloads that were updated within a specific time frame
        public async Task<string> GetRecentlyUpdatedWorkloadsAsync(string AKSClusterResourceId, string _namespace, int minutesAgo)
        {
            // 1. Validate Inputs
            if (minutesAgo <= 0)
            {
                _logger?.LogInternalWarning("minutesAgo must be positive. Received {MinutesAgo} for namespace {Namespace}", minutesAgo, _namespace);
                return $"Invalid input: minutesAgo must be a positive number (received {minutesAgo}).";
            }
            if (string.IsNullOrWhiteSpace(AKSClusterResourceId))
            {
                _logger?.LogInternalWarning("AKSClusterResourceId cannot be null or empty when checking namespace {Namespace}", _namespace);
                return "Invalid input: AKSClusterResourceId cannot be empty.";
            }
            if (string.IsNullOrWhiteSpace(_namespace))
            {
                _logger?.LogInternalWarning("Namespace cannot be null or empty.");
                return "Invalid input: Namespace cannot be empty.";
            }


            var recentlyUpdated = new List<string>();
            var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo);

            try
            {
                // 2. Get Kubernetes Client
                IKubernetes client = await GetOrCreateClientAsync(AKSClusterResourceId); // Assuming GetOrCreateClientAsync exists

                // 3. Check Deployments in the specified namespace
                _logger?.LogInternalInformation("Checking for Deployments updated since {CutoffTime} in namespace: {Namespace}",
                    cutoffTime.ToString("o"), _namespace);

                V1DeploymentList deploymentList;
                try
                {
                    deploymentList = await client.AppsV1.ListNamespacedDeploymentAsync(_namespace);
                }
                catch (Microsoft.Rest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger?.LogInternalWarning("Namespace '{TargetNamespace}' not found when checking Deployments.", _namespace);
                    return $"Namespace '{_namespace}' not found."; // Namespace doesn't exist, return error
                }
                catch (Exception ex)
                {
                    _logger?.LogInternalError(ex, "Error listing Deployments in namespace {Namespace}", _namespace);
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
                _logger?.LogInternalInformation("Checking for StatefulSets updated since {CutoffTime} in namespace: {Namespace}",
                    cutoffTime.ToString("o"), _namespace);

                V1StatefulSetList statefulSetList;
                try
                {
                    statefulSetList = await client.AppsV1.ListNamespacedStatefulSetAsync(_namespace);
                }
                catch (Microsoft.Rest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Namespace was likely already reported as not found from Deployment check, but log anyway.
                    _logger?.LogInternalWarning("Namespace '{TargetNamespace}' not found when checking StatefulSets (already checked for Deployments).", _namespace);

                    statefulSetList = new V1StatefulSetList(items: new List<V1StatefulSet>()); // Assume empty list if somehow missed earlier
                }
                catch (Exception ex)
                {
                    _logger?.LogInternalError(ex, "Error listing StatefulSets in namespace {Namespace}", _namespace);
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
                _logger?.LogInternalError(ex, "Error retrieving recently updated workloads for resourceId {ResourceId} and namespace {Namespace}", AKSClusterResourceId, _namespace);
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

        // Requires Azure Monitor for Prometheus addon to be enabled on AKS.
        private string[] BuildPromQueries(string metricType, string _namespace, string workloadType, string workloadName, string duration)
        {
            var podFilter = "";
            switch (workloadType.ToLowerInvariant())
            {
                case "deployment":
                    podFilter = $"^{workloadName}-(?:[a-z0-9]+)(?:-[a-z0-9]+)?$";
                    break;
                case "replicaset":
                    podFilter = $"^{workloadName}-(?:[a-z0-9]+)$";
                    break;
                case "daemonset":
                    podFilter = $"^{workloadName}-(?:[a-z0-9]+)$";
                    break;
                case "statefulset":
                    podFilter = $"^{workloadName}-(?:[a-z0-9]+)$";
                    break;
                default:
                    podFilter = $"^{workloadName}$";
                    break;
            }

            switch (metricType.ToLowerInvariant())
            {
                case "memory":
                    return new[] {
$@"avg(
    100 *  (
        sum by (pod) (
            container_memory_working_set_bytes{{pod=~""{podFilter}"",namespace=""{_namespace}"",container!=""""}}
        )
        / on (pod)
        min by (pod) (
            (
                kube_node_status_allocatable{{resource=""memory""}} * on (node) group_right kube_pod_info{{pod=~""{podFilter}"",namespace=""{_namespace}""}}
            )   
            or
            (
                kube_pod_container_resource_limits{{pod=~""{podFilter}"",namespace=""{_namespace}"", resource=""memory""}}
            )
        ) 
    )
)"
                    };
                case "cpu":
                    return new[] {
$@"avg(
    100 *  (
        sum by (pod) (
            rate(container_cpu_usage_seconds_total{{pod=~""{podFilter}"",namespace=""{_namespace}"",container!=""""}}[2m])
        )
        / on (pod)
        min by (pod) (
            (
                kube_node_status_allocatable{{resource=""cpu""}} * on (node) group_right kube_pod_info{{pod=~""{podFilter}"",namespace=""{_namespace}""}}
            )   
            or
            (
                kube_pod_container_resource_limits{{pod=~""{podFilter}"",namespace=""{_namespace}"", resource=""cpu""}}
            )
        ) 
    )
)"
                    };
                case "availability":
                    return new[] {
$@"100 * (
    sum (
        min by (pod) (kube_pod_container_status_ready{{pod=~""{podFilter}"",namespace=""{_namespace}""}})
    ) /
    sum (
        kube_pod_info{{pod=~""{podFilter}"",namespace=""{_namespace}""}}
    )
)"
                  };
                // Default case for custom queries or other unhandled metric types
                default:
                    _logger?.LogInternalWarning(
                        "No query configured for metric type '{MetricType}' in namespace '{Namespace}', workload type '{WorkloadType}', and workload name '{WorkloadName}'.",
                        metricType, _namespace, workloadType, workloadName);

                    return new[] { $"No query configured for metric type '{metricType}' in namespace '{_namespace}', workload type '{workloadType}', and workload name '{workloadName}'." };

            }
        }

        private static string ParseTimeRangeToDuration(string timeRange)
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

        public async Task<string> GetAPIServerStatusAsync(string resourceId, string timeRange = "10m")
        {
            if (string.IsNullOrEmpty(resourceId))
            {
                return "Error: AKS cluster Resource ID is required";
            }

            try
            {

                var metrics = new List<Agent.Core.Models.Metric>
                {
                    new Agent.Core.Models.Metric { Name = "apiserver_cpu_usage_percentage", Unit = "Percent", Aggregation = "Average" },
                    new Agent.Core.Models.Metric { Name = "apiserver_current_inflight_requests", Unit = "Count", Aggregation = "Total" },
                    new Agent.Core.Models.Metric { Name = "apiserver_memory_usage_percentage", Unit = "Percent", Aggregation = "Average"}
                };


                var sb = new StringBuilder();
                sb.AppendLine("## API Server Status");
                sb.AppendLine();

                //string displayName = GetDisplayName(metricName);
                //string unit = GetUnit(metricName);

                // Get the metric using IAzureMetricsClient
                var metricsData = await _azureMetricsClient.GetMetricsAsync(resourceId, metrics, timeRange);

                if (metricsData == null)
                {
                    sb.AppendLine("  No data available");
                }
                else
                {
                    var cpuUsage = metricsData.FirstOrDefault(m => m.Name == "apiserver_cpu_usage_percentage")?.Value ?? 0;
                    sb.AppendLine($"- apiserver cpu usage: {cpuUsage}%");
                    var memUsage = metricsData.FirstOrDefault(m => m.Name == "apiserver_memory_usage_percentage")?.Value ?? 0;
                    sb.AppendLine($"- apiserver memory usage: {memUsage}%");
                    var inflightReqs = metricsData.FirstOrDefault(m => m.Name == "apiserver_current_inflight_requests")?.Value ?? 0;
                    sb.AppendLine($"- apiserver current inflight requests: {inflightReqs}");
                }


                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Error fetching API server metrics for AKS cluster {ResourceId}", resourceId);
                return $"Error retrieving API server metrics: {ex.Message}";
            }
        }

        public async Task<string> GetEtcdStatusAsync(string resourceId, string timeRange = "10m")
        {
            if (string.IsNullOrEmpty(resourceId))
            {
                return "Error: AKS cluster Resource ID is required";
            }

            try
            {

                var metrics = new List<Agent.Core.Models.Metric>
                {
                    new Agent.Core.Models.Metric { Name = "etcd_cpu_usage_percentage", Unit = "Percent", Aggregation = "Average" },
                    new Agent.Core.Models.Metric { Name = "etcd_memory_usage_percentage", Unit = "Percent", Aggregation = "Average" },
                    new Agent.Core.Models.Metric { Name = "etcd_database_usage_percentage", Unit = "Percent", Aggregation = "Average"}
                };


                var sb = new StringBuilder();
                sb.AppendLine("## API Server Status");
                sb.AppendLine();

                //string displayName = GetDisplayName(metricName);
                //string unit = GetUnit(metricName);

                // Get the metric using IAzureMetricsClient
                var metricsData = await _azureMetricsClient.GetMetricsAsync(resourceId, metrics, timeRange);

                if (metricsData == null)
                {
                    sb.AppendLine("  No data available");
                }
                else
                {
                    var cpuUsage = metricsData.FirstOrDefault(m => m.Name == "etcd_cpu_usage_percentage")?.Value ?? 0;
                    sb.AppendLine($"- etcd cpu usage: {cpuUsage}%");
                    var memUsage = metricsData.FirstOrDefault(m => m.Name == "etcd_memory_usage_percentage")?.Value ?? 0;
                    sb.AppendLine($"- etcd memory usage: {memUsage}%");
                    var dbUsage = metricsData.FirstOrDefault(m => m.Name == "etcd_database_usage_percentage")?.Value ?? 0;
                    sb.AppendLine($"- etcd database usage: {dbUsage}%");
                }


                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Error fetching etcd metrics for AKS cluster {ResourceId}", resourceId);
                return $"Error retrieving etcd metrics: {ex.Message}";
            }
        }

        public async Task<string> GetNsgRulesForWorkloadAsync(
            string aksResourceId,
            string _namespace,
            string kind,
            string workloadName)
        {
            _logger?.LogInformation("[GetNsgRulesForWorkloadAsync] Invoked for Kind: '{Kind}', Name: '{WorkloadName}', Namespace: '{Namespace}', Cluster: '{ResourceId}'",
                kind, workloadName, _namespace, aksResourceId);

            // 1. Basic Kind Validation
            if (!kind.Equals("deployment", StringComparison.OrdinalIgnoreCase) && !kind.Equals("statefulset", StringComparison.OrdinalIgnoreCase))
            {
                string errorMsg = $"Error: Unsupported workload kind '{kind}' for GetNsgRulesForWorkloadAsync. Supported: Deployment, StatefulSet.";
                _logger?.LogError(errorMsg);
                return errorMsg;
            }

            string noRulesFoundMsg = $"No relevant NSG rules found associated with the node pools for workload '{kind}' '{workloadName}' in namespace '{_namespace}'.";
            string workloadNotFoundMsg = $"Could not determine node pools for workload '{kind}' '{workloadName}' in namespace '{_namespace}', or no relevant pods are running. Cannot fetch NSG rules.";


            try
            {
                // 2. Find the node pools for the specific workload
                _logger?.LogDebug("Step 1: Determining node pools for workload '{WorkloadName}'.", workloadName);
                // Assuming GetNodePoolsForWorkloadPodsAsync returns IReadOnlyList<string> or similar
                var nodePools = await GetNodePoolsForWorkloadPodsAsync(aksResourceId, _namespace, kind, workloadName);

                // 3. Check if node pools were found
                if (nodePools == null || !nodePools.Any())
                {
                    _logger?.LogWarning(workloadNotFoundMsg);
                    return workloadNotFoundMsg; // Return informative string
                }

                _logger?.LogInformation("Step 2: Found workload '{WorkloadName}' running on node pool(s): {NodePools}. Fetching associated NSG rules...",
                    workloadName, string.Join(", ", nodePools));

                // 4. Get NSG rules specifically for those node pools
                // Assuming GetNsgRulesForNodePoolsAsync returns IDictionary<string, IReadOnlyList<SecurityRuleData>>
                var nsgRulesDict = await GetNsgRulesForNodePoolsAsync(aksResourceId, nodePools);

                // 5. Check if NSG rules were found
                if (nsgRulesDict == null || !nsgRulesDict.Any())
                {
                    _logger?.LogInformation("Successfully retrieved NSG information, but no specific rules were found for the relevant node pools of workload '{WorkloadName}'.", workloadName);
                    return noRulesFoundMsg; // Return informative string
                }

                _logger?.LogInformation("Successfully retrieved NSG rules associated with the node pools for workload '{WorkloadName}'. Found {NsgCount} relevant NSG(s). Serializing to JSON...", workloadName, nsgRulesDict.Count);

                // 6. Serialize the result to JSON
                try
                {
                    string jsonResult = System.Text.Json.JsonSerializer.Serialize(nsgRulesDict, new JsonSerializerOptions
                    {
                        WriteIndented = true // Makes the output readable
                    });
                    return jsonResult; // Return the JSON string
                }
                catch (System.Text.Json.JsonException jsonEx)
                {
                    _logger?.LogError(jsonEx, "Failed to serialize NSG rules dictionary to JSON for workload '{WorkloadName}'.", workloadName);
                    return $"Error: Failed to format NSG rule data. {jsonEx.Message}";
                }
                catch (NotSupportedException nse) // Can happen with complex unserializable types
                {
                    _logger?.LogError(nse, "Failed to serialize NSG rules dictionary to JSON due to unsupported type for workload '{WorkloadName}'.", workloadName);
                    return $"Error: Failed to format NSG rule data due to an unsupported type. {nse.Message}";
                }
            }
            catch (Exception ex)
            {
                // Log the exception with specific details
                _logger?.LogError(ex, "An unexpected error occurred in GetNsgRulesForWorkloadAsync for Kind: '{Kind}', Name: '{WorkloadName}', Namespace: '{Namespace}'.",
                    kind, workloadName, _namespace);
                return $"Error: An unexpected error occurred while fetching NSG rules for '{kind}' '{workloadName}'. {ex.Message}"; // Return error string
            }
        }

        public async Task<IReadOnlyList<string>> GetNodePoolsForWorkloadPodsAsync(string resourceId, string _namespace, string kind, string workloadName)
        {
            _logger?.LogInformation("[GetNodePoolsForWorkloadPodsAsync] Invoked for Workload '{WorkloadName}' type '{WorkloadType}' in namespace '{Namespace}' on cluster '{ResourceId}'", workloadName, kind, _namespace, resourceId);
            var nodePools = new HashSet<string>(); // Use HashSet for automatic deduplication

            // Ensure kind is supported
            if (!kind.Equals("deployment", StringComparison.OrdinalIgnoreCase) && !kind.Equals("statefulset", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogError("Unsupported workload kind '{Kind}' for node pool lookup. Supported: Deployment, StatefulSet.", kind);
                return new List<string>().AsReadOnly();
            }

            try
            {
                // 1. Get Kubernetes Client
                _client = await GetOrCreateClientAsync(resourceId);

                // 2. Get the Deployment/StatefulSet to find its label selector
                V1ObjectMeta metadata = null;
                V1LabelSelector selector = null;
                string workloadKindLogName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(kind.ToLowerInvariant()); // "Deployment" or "StatefulSet"

                switch (kind.ToLowerInvariant())
                {
                    case "deployment":
                        V1Deployment deploy;
                        try
                        {
                            deploy = await _client.AppsV1.ReadNamespacedDeploymentAsync(workloadName, _namespace);
                            metadata = deploy?.Metadata;
                            selector = deploy?.Spec?.Selector;
                            _logger?.LogInformation("Successfully retrieved Deployment '{DeploymentName}'", workloadName);
                        }
                        catch (Microsoft.Rest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            _logger?.LogWarning("Deployment '{DeploymentName}' not found in namespace '{Namespace}'.", workloadName, _namespace);
                            return new List<string>().AsReadOnly();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error retrieving Deployment '{DeploymentName}' in namespace '{Namespace}'.", workloadName, _namespace);
                            return new List<string>().AsReadOnly();
                        }
                        break;
                    case "statefulset":
                        V1StatefulSet sts;
                        try
                        {
                            sts = await _client.AppsV1.ReadNamespacedStatefulSetAsync(workloadName, _namespace);
                            metadata = sts?.Metadata;
                            selector = sts?.Spec?.Selector;
                            _logger?.LogInformation("Successfully retrieved StatefulSet '{StatefulSetName}'", workloadName);
                        }
                        catch (Microsoft.Rest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            _logger?.LogWarning("StatefulSet '{StatefulSetName}' not found in namespace '{Namespace}'.", workloadName, _namespace);
                            return new List<string>().AsReadOnly();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error retrieving StatefulSet '{StatefulSetName}' in namespace '{Namespace}'.", workloadName, _namespace);
                            return new List<string>().AsReadOnly();
                        }
                        break;

                    default: return new List<string>().AsReadOnly();
                }


                if (selector?.MatchLabels == null || !selector.MatchLabels.Any())
                {
                    _logger?.LogWarning("{WorkloadType} '{WorkloadName}' does not have valid spec.selector.matchLabels.", workloadKindLogName, workloadName);
                    return new List<string>().AsReadOnly();
                }

                // 3. Construct the label selector string
                string labelSelectorString = string.Join(",", selector.MatchLabels.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                _logger?.LogInformation("Using label selector: '{LabelSelector}'", labelSelectorString);

                // 4. List Pods matching the selector
                V1PodList podList;
                try
                {
                    podList = await _client.CoreV1.ListNamespacedPodAsync(_namespace, labelSelector: labelSelectorString);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error listing pods with selector '{LabelSelector}' in namespace '{Namespace}'.", labelSelectorString, _namespace);
                    return new List<string>().AsReadOnly();
                }

                // 5. Filter for Running pods and get distinct node names
                var runningPodNodeNames = podList.Items
                    .Where(p => p.Status?.Phase == "Running" && !string.IsNullOrEmpty(p.Spec.NodeName))
                    .Select(p => p.Spec.NodeName)
                    .Distinct() // Process each node only once
                    .ToList();


                if (!runningPodNodeNames.Any())
                {
                    _logger?.LogWarning("No 'Running' pods found for {WorkloadType} '{WorkloadName}' with selector '{LabelSelector}' in namespace '{Namespace}', or none are scheduled.", workloadKindLogName, workloadName, labelSelectorString, _namespace);
                    return new List<string>().AsReadOnly();
                }

                _logger?.LogInformation("Found {RunningPodCount} running pod(s) for {WorkloadType} '{WorkloadName}' on {NodeCount} distinct node(s). Checking node pool labels...",
                    podList.Items.Count(p => p.Status?.Phase == "Running"), workloadKindLogName, workloadName, runningPodNodeNames.Count);

                // 6. Iterate through distinct nodes hosting the pods
                foreach (var nodeName in runningPodNodeNames)
                {
                    _logger?.LogDebug("Checking labels for Node '{NodeName}'", nodeName);

                    try
                    {
                        // 7. Get the Node object
                        var node = await _client.CoreV1.ReadNodeAsync(nodeName);

                        // 8. Extract the node pool label
                        string? nodePoolName = null;
                        if (node.Metadata?.Labels != null)
                        {
                            if (node.Metadata.Labels.TryGetValue(AKSNodePoolLabel, out nodePoolName) ||
                                node.Metadata.Labels.TryGetValue(LegacyAKSNodePoolLabel, out nodePoolName))
                            {
                                _logger?.LogDebug("Node '{NodeName}' belongs to Node Pool '{NodePoolName}'", nodeName, nodePoolName);
                                nodePools.Add(nodePoolName); // HashSet handles duplicates
                            }
                            else
                            {
                                _logger?.LogWarning("Node '{NodeName}' does not have a recognizable node pool label ('{Label1}' or '{Label2}').",
                                                   nodeName, AKSNodePoolLabel, LegacyAKSNodePoolLabel);
                            }
                        }
                        else
                        {
                            _logger?.LogWarning("Node '{NodeName}' has no labels.", nodeName);
                        }
                    }
                    catch (Microsoft.Rest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger?.LogWarning("Node '{NodeName}' not found via Kubernetes API.", nodeName);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error retrieving or processing Node '{NodeName}'.", nodeName);
                    }
                } // End foreach nodeName

                _logger?.LogInformation("Finished processing nodes for {WorkloadType} '{WorkloadName}'. Found {NodePoolCount} distinct node pool(s): {NodePoolList}", workloadKindLogName, workloadName, nodePools.Count, string.Join(", ", nodePools));
                return nodePools.ToList().AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "An unexpected error occurred in GetNodePoolsForWorkloadPodsAsync for Workload '{WorkloadName}' type '{WorkloadType}' in namespace '{Namespace}'.", workloadName, kind, _namespace);
                return new List<string>().AsReadOnly();
            }
        }

        public async Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetNsgRulesForNodePoolsAsync(
        string aksResourceId,
        IReadOnlyList<string> targetNodePoolNames)
        {
            _logger?.LogInformation("[GetNsgRulesForNodePoolsAsync] Invoked for cluster '{ResourceId}' and Node Pools: {NodePoolNames}",
                aksResourceId, string.Join(", ", targetNodePoolNames ?? new List<string>()));

            var result = new Dictionary<string, IReadOnlyList<SecurityRuleData>>();

            // Basic input validation
            if (targetNodePoolNames == null || !targetNodePoolNames.Any())
            {
                _logger?.LogWarning("No target node pool names provided. Returning empty NSG rule set.");
                return result;
            }

            // Use a HashSet for efficient lookup of target node pool names
            var targetNodePoolSet = new HashSet<string>(targetNodePoolNames, StringComparer.OrdinalIgnoreCase);

            try
            {
                // 1. Get the AKS Managed Cluster Resource
                var aksClusterResource = _armClient.GetContainerServiceManagedClusterResource(new ResourceIdentifier(aksResourceId));
                var aksClusterResponse = await aksClusterResource.GetAsync();

                if (!aksClusterResponse.HasValue || aksClusterResponse.Value.Data == null)
                {
                    _logger?.LogWarning($"AKS Cluster data not found for resourceId: {aksResourceId}");
                    return result; // Return empty dictionary
                }
                var aksClusterData = aksClusterResponse.Value.Data;

                // 2. Check and Filter Agent Pool Profiles
                if (aksClusterData.AgentPoolProfiles == null || !aksClusterData.AgentPoolProfiles.Any())
                {
                    _logger?.LogWarning($"AKS Cluster {aksResourceId} has no agent pool profiles defined.");
                    return result;
                }

                // Filter the agent pools to only those specified in the input list
                var relevantAgentPools = aksClusterData.AgentPoolProfiles
                    .Where(pool => pool?.Name != null && targetNodePoolSet.Contains(pool.Name))
                    .ToList();

                if (!relevantAgentPools.Any())
                {
                    _logger?.LogWarning($"No agent pools found in cluster '{aksResourceId}' matching the target names: {string.Join(", ", targetNodePoolNames)}");
                    return result;
                }

                _logger?.LogInformation("Found {RelevantPoolCount} relevant agent pool(s) matching target names. Checking their subnets...", relevantAgentPools.Count);

                // 3. Iterate through the *filtered* agent pools
                foreach (var agentPool in relevantAgentPools)
                {
                    // 4. Get Subnet ID
                    if (agentPool.VnetSubnetId == null || string.IsNullOrEmpty(agentPool.VnetSubnetId.ToString()))
                    {
                        _logger?.LogInformation($"Target agent pool '{agentPool.Name}' in AKS Cluster {aksResourceId} does not have a VNet Subnet ID specified or is not VNet integrated.");
                        continue; // Skip this agent pool
                    }

                    string subnetId = agentPool.VnetSubnetId.ToString();
                    _logger?.LogInformation($"Processing subnet '{subnetId}' for target agent pool '{agentPool.Name}'");

                    try
                    {
                        // 5. Get the Subnet Resource
                        var subnetResource = _armClient.GetSubnetResource(new ResourceIdentifier(subnetId));
                        var subnetResponse = await subnetResource.GetAsync();

                        if (!subnetResponse.HasValue || subnetResponse.Value.Data == null)
                        {
                            _logger?.LogWarning($"Could not retrieve data for subnet '{subnetId}' used by agent pool '{agentPool.Name}'. Skipping.");
                            continue;
                        }
                        var subnetData = subnetResponse.Value.Data;

                        // 6. Check if Subnet has an NSG associated
                        if (subnetData.NetworkSecurityGroup != null && subnetData.NetworkSecurityGroup.Id != null)
                        {
                            string nsgId = subnetData.NetworkSecurityGroup.Id.ToString();

                            // 7. Avoid processing the same NSG multiple times (optimization)
                            if (!result.ContainsKey(nsgId))
                            {
                                _logger?.LogInformation($"Found NSG '{nsgId}' associated with subnet '{subnetId}'. Fetching rules...");

                                // 8. Get the NSG Resource and its rules
                                var nsgResource = _armClient.GetNetworkSecurityGroupResource(new ResourceIdentifier(nsgId));
                                var nsgResponse = await nsgResource.GetAsync();

                                if (!nsgResponse.HasValue || nsgResponse.Value.Data == null)
                                {
                                    _logger?.LogWarning($"Could not retrieve data for NSG '{nsgId}'. Skipping NSG rule fetch for this subnet.");
                                    // Optionally, add the NSG ID with an empty list or some error indicator
                                    // result.Add(nsgId, new List<SecurityRuleData>().AsReadOnly());
                                    continue;
                                }
                                var nsgData = nsgResponse.Value.Data;

                                if (nsgData.SecurityRules != null)
                                {
                                    // Store the rules as a read-only list
                                    result[nsgId] = nsgData.SecurityRules.ToList().AsReadOnly();
                                    _logger?.LogInformation($"Added {nsgData.SecurityRules.Count} rules from NSG '{nsgId}'");
                                }
                                else
                                {
                                    _logger?.LogInformation($"NSG '{nsgId}' found but has no security rules defined.");
                                    result[nsgId] = new List<SecurityRuleData>().AsReadOnly(); // Add empty list
                                }
                            }
                            else
                            {
                                _logger?.LogInformation($"NSG '{nsgId}' associated with subnet '{subnetId}' (Agent Pool '{agentPool.Name}') was already processed.");
                            }
                        }
                        else
                        {
                            _logger?.LogInformation($"No NSG found directly associated with subnet '{subnetId}' for agent pool '{agentPool.Name}'.");
                        }
                    }
                    catch (Exception subEx)
                    {
                        _logger?.LogError(subEx, $"Error processing subnet '{subnetId}' for agent pool '{agentPool.Name}' in AKS Cluster {aksResourceId}");
                        // Decide whether to continue with other pools or stop (currently continues)
                    }
                } // End foreach agentPool

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"An unexpected error occurred in GetNsgRulesForNodePoolsAsync for cluster {aksResourceId}");
                return result; // Return whatever was collected before the error
            }
        }

        public async Task<string> ListWorkloadRevisions(string resourceId, string _namespace, string kind, string name)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            var sb = new StringBuilder();

            try
            {
                switch (kind.ToLowerInvariant())
                {
                    case "deployment":
                        // For deployments, we need to get all ReplicaSets and filter those owned by our deployment
                        var deployment = await _client.AppsV1.ReadNamespacedDeploymentAsync(name, _namespace);
                        if (deployment == null)
                        {
                            return $"Deployment '{name}' not found in namespace '{_namespace}'";
                        }

                        var replicaSets = await _client.AppsV1.ListNamespacedReplicaSetAsync(
                            _namespace,
                            labelSelector: string.Join(",", deployment.Spec.Selector.MatchLabels.Select(l => $"{l.Key}={l.Value}")));

                        // Filter ReplicaSets owned by our deployment and sort by revision number
                        var ownedReplicaSets = replicaSets.Items
                            .Where(rs => rs.Metadata.OwnerReferences != null &&
                                       rs.Metadata.OwnerReferences.Any(or => or.Kind == "Deployment" &&
                                                                          or.Name == name))
                            .OrderByDescending(rs => int.TryParse(rs.Metadata.Annotations?["deployment.kubernetes.io/revision"], out int rev) ? rev : 0)
                            .ToList();

                        if (!ownedReplicaSets.Any())
                        {
                            return $"No revisions found for Deployment '{name}' in namespace '{_namespace}'";
                        }

                        foreach (var rs in ownedReplicaSets)
                        {
                            string revisionNumber = rs.Metadata.Annotations?["deployment.kubernetes.io/revision"] ?? "unknown";
                            sb.AppendLine($"## Revision {revisionNumber}");
                            sb.AppendLine($"Created: {rs.Metadata.CreationTimestamp:yyyy-MM-dd HH:mm:ss}");
                            sb.AppendLine("```yaml");
                            sb.AppendLine(YamlHelper.Serialize(rs));
                            sb.AppendLine("```");
                            sb.AppendLine();
                        }
                        break;

                    case "statefulset":
                        // For StatefulSets, we use ControllerRevision objects
                        var statefulSet = await _client.AppsV1.ReadNamespacedStatefulSetAsync(name, _namespace);
                        if (statefulSet == null)
                        {
                            return $"StatefulSet '{name}' not found in namespace '{_namespace}'";
                        }

                        var controllerRevisions = await _client.AppsV1.ListNamespacedControllerRevisionAsync(
                            _namespace,
                            labelSelector: string.Join(",", statefulSet.Spec.Selector.MatchLabels.Select(l => $"{l.Key}={l.Value}")));

                        // Filter ControllerRevisions owned by our StatefulSet and sort by revision number
                        var ownedRevisions = controllerRevisions.Items
                            .Where(cr => cr.Metadata.OwnerReferences != null &&
                                       cr.Metadata.OwnerReferences.Any(or => or.Kind == "StatefulSet" &&
                                                                          or.Name == name))
                            .OrderByDescending(cr => cr.Revision)
                            .ToList();

                        if (!ownedRevisions.Any())
                        {
                            return $"No revisions found for StatefulSet '{name}' in namespace '{_namespace}'";
                        }

                        foreach (var revision in ownedRevisions)
                        {
                            sb.AppendLine($"## Revision {revision.Revision}");
                            sb.AppendLine($"Created: {revision.Metadata.CreationTimestamp:yyyy-MM-dd HH:mm:ss}");
                            sb.AppendLine("```yaml");
                            sb.AppendLine(YamlHelper.Serialize(revision));
                            sb.AppendLine("```");
                            sb.AppendLine();
                        }
                        break;

                    default:
                        return $"Workload kind '{kind}' is not supported for revision listing. Only Deployment and StatefulSet are supported.";
                }

                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Error listing revisions for {Kind} '{Name}' in namespace {Namespace}", kind, name, _namespace);
                return $"Error listing revisions: {ex.Message}";
            }
        }
    }
}
