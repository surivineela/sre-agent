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
using Agent.Core.Models;
using Microsoft.Extensions.AI;
using Agent.Prometheus.Services;
using Agent.Core.Configuration;
using Agent.Prometheus;
using Agent.Graph.Crawler.Metrics;
using Agent.Core.Services;

namespace Agent.Plugins
{
    public partial class KubePlugin : IKubePlugin
    {
        private readonly ILogger? _logger;
        private IKubernetes _client;
        private IChatClient _chatClient;

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

        public KubePlugin(
            IAuthenticationService authenticationService,
            IChatClient chatClient,
            IPrometheusQueryService prometheusQueryService,
            IAzureMetricsClient azureMetricsClient,
            DashboardSettings dashboardSettings,
            IKubernetesClientFactory kubernetesClientFactory,
            ILogger<KubePlugin>? logger)
        {
            _logger = logger;
            _authService = authenticationService;
            _chatClient = chatClient;
            _prometheusQueryService = prometheusQueryService;
            _dashboardSettings = dashboardSettings;
            _azureMetricsClient = azureMetricsClient;
            _kubernetesClientFactory = kubernetesClientFactory;

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

        // get all statefulsets in a namespace
        public async Task<string> GetKubeStatefulsetsAsync(string resourceId, string _namespace)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            var statefulSetList = await _client.AppsV1.ListNamespacedStatefulSetAsync(_namespace);
            var statefulSetNames = statefulSetList.Items.Select(sts => sts.Metadata.Name);

            return string.Join(", ", statefulSetNames);
        }

        public async Task<string> DiagnoseAKSAppAsync(string resourceId, string _namespace, string kind, string name)
        {
            _logger?.LogInformation("Diagnosing {Kind} {Name} in namespace {Namespace}", kind, name, _namespace);
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
            tasks.Add(GetCpuMetricsForWorkloadAsync(resourceId, _namespace, kind, name)
            .ContinueWith(task => results["CpuMetrics"] = task.Result));

            tasks.Add(GetMemoryMetricsForWorkloadAsync(resourceId, _namespace, kind, name)
                .ContinueWith(task => results["MemoryMetrics"] = task.Result));

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
                _logger.LogInformation("Diagnosing pod: {Pod} for component: {Name}", pod, name);
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
                _logger?.LogError(ex, "Error summarizing logs with chat service");
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
                _logger?.LogError(ex, "Error listing custom resources");
                return $"Error listing custom resources: {ex.Message}";
            }
        }
        // TODO(jianbosun): need to validate and expose to KubePluginDefinition
        public async Task<string> GetKubeResourceMetricsRangeAsync(string AKSClusterResourceId, string _namespace, string kind, string name, string metricsType, string rawDuration, string startTime, string endTime)
        {

            // Convert the provided time range into Prometheus-compatible duration format
            string duration = ParseTimeRangeToDuration(rawDuration);

            // Build the PromQL query based on the specified metric type
            string query = BuildPromQuery(metricsType, _namespace, kind, name, duration);

            if (string.IsNullOrEmpty(query) || query.StartsWith("No query", StringComparison.OrdinalIgnoreCase))
            {
                var errorResponse = $"Failed to build a valid PromQL query for metric type '{metricsType}' in namespace '{_namespace}', workload type '{kind}', and workload name '{name}'";
                _logger?.LogWarning(
                   errorResponse,
                    metricsType, _namespace, kind, name);
                return errorResponse;
            }

            _logger?.LogInformation(
                "Executing PromQL against Azure Monitor Prometheus endpoint '{Endpoint}': {Query}",
                _prometheusQueryEndpoint, query);

            DateTime startDate = ParseDateTime(startTime);
            DateTime endDate = ParseDateTime(endTime);
            TimeSpan step = ParseTimeSpan(duration);

            // Query the Prometheus endpoint using the injected service
            var response = await _prometheusQueryService.QueryRangeAsync(_prometheusQueryEndpoint, query, startDate, endDate, step);
            return FormatPrometheusRangeResponse(response, metricsType, kind, name, startTime, endTime, rawDuration);
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
            _logger?.LogWarning("Failed to parse time string: {TimeDate}. Using current time.", timeDate);
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
                            _logger?.LogError(ex, $"Error getting custom resource {kind}/{name} in namespace {_namespace}");
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
                _logger?.LogError(ex, $"Error getting events for {kind}, {name}");
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
                _logger?.LogError(ex, $"Error getting spec status for {kind}, {name} ");
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
                string query = BuildPromQuery(metricType, _namespace, workloadType, workloadName, duration);

                if (string.IsNullOrEmpty(query) || query.StartsWith("No query", StringComparison.OrdinalIgnoreCase))
                {

                    var errorResponse = $"Failed to build a valid PromQL query for metric type '{metricType}' in namespace '{_namespace}', workload type '{workloadType}', and workload name '{workloadName}'";
                    _logger?.LogWarning(
                        errorResponse,
                        metricType, _namespace, workloadType, workloadName);
                    return errorResponse;
                }

                _logger?.LogInformation(
                    "Executing PromQL against Azure Monitor Prometheus endpoint '{Endpoint}': {Query}",
                    _prometheusQueryEndpoint, query);

                // Query the Prometheus endpoint using the injected service
                var response = await _prometheusQueryService.QueryInstantAsync(_prometheusQueryEndpoint, query);

                return FormatPrometheusResponse(response, metricType, workloadType, workloadName);
            }
            catch (HttpRequestException httpEx)
            {
                _logger?.LogError(
                    httpEx,
                    "HTTP error while querying Azure Monitor Prometheus for metric type '{MetricType}' in namespace '{Namespace}', workload type '{WorkloadType}', and workload name '{WorkloadName}'.",
                    metricType, _namespace, workloadType, workloadName);
                return $"HTTP error while querying Azure Monitor Prometheus: {httpEx.Message} (StatusCode: {httpEx.StatusCode})";
            }
            catch (Exception ex)
            {
                _logger?.LogError(
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

        private static string FormatPrometheusRangeResponse(Response? response, string metricType, string workloadType, string workloadName, string rawDuration, string startTime, string endTime)
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
                    sb.AppendLine($"## {capitalizedMetricType} Usage for workloadType {workloadType} and workloadName {workloadName} during timestamp from {startTime} to {endTime}");
                    sb.AppendLine();

                    foreach (var resultItem in matrixData.Result)
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

                        // --- Metric Value ---
                        foreach (var metricItem in resultItem.Values)
                        {
                            double timestamp = metricItem.Item1; // Unix timestamp (seconds)
                            string rawValue = metricItem.Item2;
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

        // Requires Azure Monitor for Prometheus addon to be enabled on AKS.
        private string BuildPromQuery(string metricType, string _namespace, string workloadType, string workloadName, string duration)
        {
            var pod = workloadName;
            if (!workloadType.Equals("pod", StringComparison.OrdinalIgnoreCase))
            {
                pod = $"{workloadName}-.*";
            }
            switch (metricType.ToLowerInvariant())
            {
                case "memory":
                    return $@"
                            100 *  (
                                sum by (pod) (
                                    container_memory_working_set_bytes{{pod=~""{pod}"",namespace=""{_namespace}"",container!=""""}}
                                )
                                / on (pod)
                                min by (pod) (
                                    (
                                        kube_node_status_allocatable{{resource=""memory""}} * on (node) group_right kube_pod_info{{pod=~""{pod}"",namespace=""{_namespace}""}}
                                    )   
                                    or
                                    (
                                        kube_pod_container_resource_limits{{pod=~""{pod}"",namespace=""{_namespace}"", resource=""memory""}}
                                    )
                                ) 
                            )
                        ";
                case "cpu":
                    return $@"
                            100 *  (
                                sum by (pod) (
                                    rate(container_cpu_usage_seconds_total{{pod=~""{pod}"",namespace=""{_namespace}"",container!=""""}}[2m])
                                )
                                / on (pod)
                                min by (pod) (
                                    (
                                        kube_node_status_allocatable{{resource=""cpu""}} * on (node) group_right kube_pod_info{{pod=~""{pod}"",namespace=""{_namespace}""}}
                                    )   
                                    or
                                    (
                                        kube_pod_container_resource_limits{{pod=~""{pod}"",namespace=""{_namespace}"", resource=""cpu""}}
                                    )
                                ) 
                            )
                        ";


                // Default case for custom queries or other unhandled metric types
                default:
                    _logger?.LogWarning(
                        "No query configured for metric type '{MetricType}' in namespace '{Namespace}', workload type '{WorkloadType}', and workload name '{WorkloadName}'.",
                        metricType, _namespace, workloadType, workloadName);

                    return $"No query configured for metric type '{metricType}' in namespace '{_namespace}', workload type '{workloadType}', and workload name '{workloadName}'.";

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
                _logger?.LogError(ex, "Error fetching API server metrics for AKS cluster {ResourceId}", resourceId);
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
                _logger?.LogError(ex, "Error fetching etcd metrics for AKS cluster {ResourceId}", resourceId);
                return $"Error retrieving etcd metrics: {ex.Message}";
            }
        }

    }
}
