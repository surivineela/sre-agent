// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Core;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.Metrics;
using Agent.Logging;
using Agent.Plugins.Interface;
using Agent.Prometheus;
using Agent.Prometheus.Services;
using k8s;
using k8s.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

namespace Agent.Plugins
{
    public partial class KubePlugin : IKubePlugin
    {
        private readonly ILogger? _logger;
        private readonly IChatClient _chatClient;
        private readonly IKubernetesClientFactory _kubernetesClientFactory;
        private readonly IPrometheusQueryService _prometheusQueryService;
        private readonly IAzureMetricsClient _azureMetricsClient;
        private readonly IGraphDatabaseClient? _graphDbClient;
        private readonly IArmClientFactory _armClientFactory;
        private readonly string _agentKubeCtlIdentity;

        private static readonly ISerializer _configJsonSerializer = new SerializerBuilder().ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull).Build();

        private ThreadContext Context { get; set; }
        private const string AKSNodePoolLabel = "kubernetes.azure.com/agentpool";
        private const string LegacyAKSNodePoolLabel = "agentpool";

        public KubePlugin(
            IChatClient chatClient,
            IPrometheusQueryService prometheusQueryService,
            IAzureMetricsClient azureMetricsClient,
            IKubernetesClientFactory kubernetesClientFactory,
            IArmClientFactory armClientFactory,
            IGraphDatabaseClient graphDbClient,
            IThreadRepository threadRepository,
            IAuthenticationService authenticationService,
            IHostEnvironment hostEnvironment,
            ILogger<KubePlugin>? logger)
        {
            _logger = logger;
            _chatClient = chatClient;
            _prometheusQueryService = prometheusQueryService;
            _azureMetricsClient = azureMetricsClient;
            _kubernetesClientFactory = kubernetesClientFactory;
            _graphDbClient = graphDbClient;
            _armClientFactory = armClientFactory;
            _threadRepository = threadRepository;
            _agentKubeCtlIdentity = GetAgentKubectlIdentity(authenticationService, hostEnvironment);
        }

        private static string GetAgentKubectlIdentity(
            IAuthenticationService authenticationService,
            IHostEnvironment hostEnvironment)
        {
            if (hostEnvironment.IsDevelopment())
            {
                return "your developer identity";
            }

            var agentIdentity = authenticationService.GetActionIdentity();
            if (string.IsNullOrEmpty(agentIdentity))
            {
                return "<failed to retrieve operation identity>";
            }

            if (string.Equals(Constants.SystemManagedIdentityName, agentIdentity, StringComparison.OrdinalIgnoreCase))
            {
                return "SRE Agent System Managed Identity";
            }

            return $"SRE Agent User-Assigned Identity {agentIdentity}";
        }

        public async Task<IKubernetes> GetOrCreateClientAsync(string? resourceId = null)
        {
            if (string.IsNullOrEmpty(resourceId))
            {
                throw new ArgumentException("AKS resource ID is required.");
            }

            var client = await _kubernetesClientFactory.CreateKubernetesClientFromResourceIdAsync(resourceId);
            if (client == null)
            {
                throw new InvalidOperationException($"Failed to create Kubernetes client for resourceId: {resourceId}");
            }

            return client;
        }

        public Task<string> GetAKSClusterResourceIdAsync(string Subscription, string ResourceGroupName, string AKSClusterName)
        {
            return Task.FromResult($"AKSClusterResourceID is **'/subscriptions/{Subscription}/resourceGroups/{ResourceGroupName}/providers/Microsoft.ContainerService/managedClusters/{AKSClusterName}'**");
        }

        // get all namespaces in the cluster
        public async Task<string> GetKubeNamespacesAsync(string resourceId)
        {
            var client = await GetOrCreateClientAsync(resourceId);
            var namespaces = await client.CoreV1.ListNamespaceAsync();
            var namespaceNames = namespaces.Items.Select(ns => ns.Metadata.Name);

            return string.Join(", ", namespaceNames);
        }

        // get all deployments in a namespace
        public async Task<string> GetKubeDeploymentsAsync(string resourceId, string _namespace)
        {
            var client = await GetOrCreateClientAsync(resourceId);
            var deployments = await client.AppsV1.ListNamespacedDeploymentAsync(_namespace);
            var deploymentNames = deployments.Items.Select(deployment => deployment.Metadata.Name);

            return string.Join(", ", deploymentNames);
        }

        // get all resource objects in a namespace with specific kind
        public async Task<string> ListKubeResourcesAsync(string resourceId, string? _namespace, string kind)
        {
            var client = await GetOrCreateClientAsync(resourceId);
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
                    var services = await client.CoreV1.ListNamespacedServiceAsync(_namespace);
                    nameList = services.Items.Select(service => service.Metadata.Name);
                    break;
                case "daemonset":
                case "daemonsets":
                    var ds = await client.AppsV1.ListNamespacedDaemonSetAsync(_namespace);
                    nameList = ds.Items.Select(ds => ds.Metadata.Name);
                    break;
                case "statefulset":
                case "statefulsets":
                    var statefulSetList = await client.AppsV1.ListNamespacedStatefulSetAsync(_namespace);
                    nameList = statefulSetList.Items.Select(sts => sts.Metadata.Name);
                    break;
                case "pod":
                case "pods":
                    var pods = await client.CoreV1.ListNamespacedPodAsync(_namespace);
                    nameList = pods.Items.Select(pod => pod.Metadata.Name);
                    break;
                case "job":
                case "jobs":
                    var jobs = await client.BatchV1.ListNamespacedJobAsync(_namespace);
                    nameList = jobs.Items.Select(job => job.Metadata.Name);
                    break;
                case "configmap":
                case "configmaps":
                    var configMaps = await client.CoreV1.ListNamespacedConfigMapAsync(_namespace);
                    nameList = configMaps.Items.Select(cm => cm.Metadata.Name);
                    break;
                case "secret":
                case "secrets":
                    var secrets = await client.CoreV1.ListNamespacedSecretAsync(_namespace);
                    nameList = secrets.Items.Select(secret => secret.Metadata.Name);
                    break;
                case "ingress":
                case "ingresses":
                    var ingresses = await client.NetworkingV1.ListNamespacedIngressAsync(_namespace);
                    nameList = ingresses.Items.Select(ingress => ingress.Metadata.Name);
                    break;
                case "replicaset":
                case "replicasets":
                    var replicaSets = await client.AppsV1.ListNamespacedReplicaSetAsync(_namespace);
                    nameList = replicaSets.Items.Select(rs => rs.Metadata.Name);
                    break;
                case "node":
                case "nodes":
                    var nodes = await client.CoreV1.ListNodeAsync();
                    nameList = nodes.Items.Select(node => node.Metadata.Name);
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
                    .ContinueWith(task => podResults[pod]["PodYaml"] = task.IsCompletedSuccessfully ? task.Result : task.Exception!.ToString()));

                podTasks.Add(GetKubeResourceEventsAsync(resourceId, _namespace, "", "Pod", pod)
                    .ContinueWith(task => podResults[pod]["Events"] = task.IsCompletedSuccessfully ? task.Result : task.Exception!.ToString()));

                podTasks.Add(GetKubePodLogsAsync(resourceId, _namespace, pod)
                    .ContinueWith(task => podResults[pod]["Logs"] = task.IsCompletedSuccessfully ? task.Result : task.Exception!.ToString()));
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
            var client = await GetOrCreateClientAsync(resourceId);
            IDictionary<string, string> labels = new Dictionary<string, string>();
            switch (kind.ToLowerInvariant())
            {
                case "deployment":
                    // get deployment in namespace
                    var deploy = await client.AppsV1.ReadNamespacedDeploymentAsync(name, _namespace);
                    // extract pod spec labels in the deployment
                    labels = deploy.Spec.Template.Metadata.Labels;
                    break;
                case "statefulset":
                    // Fallback to redis deployment if specified
                    var sts = await client.AppsV1.ReadNamespacedStatefulSetAsync(name, _namespace);
                    labels = sts.Spec.Template.Metadata.Labels;
                    break;
                default:
                    return "Unsupported kind. Only Deployment and StatefulSet are supported.";
            }
            var pods = await client.CoreV1.ListNamespacedPodAsync(_namespace, labelSelector: $"{string.Join(",", labels.Select(label => $"{label.Key}={label.Value}"))}");
            var podNames = pods.Items.Select(pod => pod.Metadata.Name);
            return string.Join(", ", podNames);
        }

        // get spec and status of a node
        public async Task<string> GetKubeNodeSpecStatusAsync(string resourceId, string nodeName)
        {
            var client = await GetOrCreateClientAsync(resourceId);
            // get node in namespace
            var node = await client.CoreV1.ReadNodeAsync(nodeName);
            if (node == null)
            {
                return "Node not found";
            }

            // Serialize to YAML
            return YamlHelper.Serialize(node);
        }

        // get spec and status of a deployment in a namespace
        public async Task<string> GetKubeDeploymentSpecStatusAsync(string resourceId, string _namespace, string deployment)
        {
            var client = await GetOrCreateClientAsync(resourceId);
            // get deployment in namespace
            var deploy = await client.AppsV1.ReadNamespacedDeploymentAsync(deployment, _namespace);
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
            var client = await GetOrCreateClientAsync(resourceId);
            // get replicaset in namespace
            var rs = await client.AppsV1.ReadNamespacedReplicaSetAsync(replicaset, _namespace);
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
            var client = await GetOrCreateClientAsync(resourceId);
            // get deployment in namespace
            var deploy = await client.AppsV1.ReadNamespacedStatefulSetAsync(deployment, _namespace);
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
            var client = await GetOrCreateClientAsync(resourceId);
            // get daemonset in namespace
            var ds = await client.AppsV1.ReadNamespacedDaemonSetAsync(daemonset, _namespace);
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
            var client = await GetOrCreateClientAsync(resourceId);
            // get pod in namespace
            var podObj = await client.CoreV1.ReadNamespacedPodAsync(pod, _namespace);
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
            var client = await GetOrCreateClientAsync(resourceId);
            // get deployment in namespace
            var deploy = await client.AppsV1.ReadNamespacedDeploymentAsync(deployment, _namespace);
            if (deploy == null)
            {
                return "Deployment not found";
            }

            // get events of this deployment
            var events = await client.CoreV1.ListNamespacedEventAsync(_namespace, fieldSelector: $"involvedObject.name={deployment},involvedObject.uid={deploy.Metadata.Uid}");
            var eventDescriptions = events.Items.Select(e => e.Message);
            return string.Join(", ", eventDescriptions);
        }

        // show events of a statefulset in a namespace
        public async Task<string> GetKubeStatefulSetEventsAsync(string resourceId, string _namespace, string deployment)
        {
            var client = await GetOrCreateClientAsync(resourceId);
            // get deployment in namespace
            var deploy = await client.AppsV1.ReadNamespacedStatefulSetAsync(deployment, _namespace);
            if (deploy == null)
            {
                return "Deployment not found";
            }

            // get events of this deployment
            var events = await client.CoreV1.ListNamespacedEventAsync(_namespace, fieldSelector: $"involvedObject.name={deployment},involvedObject.uid={deploy.Metadata.Uid}");
            var eventDescriptions = events.Items.Select(e => e.Message);
            return string.Join(", ", eventDescriptions);
        }

        // rollout restart a deployment in a namespace
        public async Task<string> RolloutRestartDeploymentAsync(string resourceId, string _namespace, string deployment)
        {
            var client = await GetOrCreateClientAsync(resourceId);
            // get deployment in namespace
            var deploy = await client.AppsV1.ReadNamespacedDeploymentAsync(deployment, _namespace);
            if (deploy == null)
            {
                return "Deployment not found";
            }

            // patch the deployment to trigger a rollout restart
            var patch = new V1Patch("{\"spec\":{\"template\":{\"metadata\":{\"annotations\":{\"sreAgent/restartedAt\":\"" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) + "\"}}}}}", V1Patch.PatchType.MergePatch);
            await client.AppsV1.PatchNamespacedDeploymentAsync(patch, deployment, _namespace);
            return "Deployment restarted";
        }

        // Scale a deployment in a namespace to the specified replica count
        public async Task<string> ScaleDeploymentAsync(string resourceId, string _namespace, string deployment, int replicaCount, string agentmode)
        {
            try
            {
                if (agentmode == ActionMode.ReadOnly.ToString())
                {
                    return $"kubectl -n <>{_namespace}> scale deployment {deployment} --replicas={replicaCount}";
                }


                if (replicaCount < 0)
                {
                    return "Replica count must be a non-negative integer";
                }

                var client = await GetOrCreateClientAsync(resourceId);

                // Get deployment in namespace to verify it exists
                var deploy = await client.AppsV1.ReadNamespacedDeploymentAsync(deployment, _namespace);
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
                var patchResult = await client.AppsV1.PatchNamespacedDeploymentAsync(
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

                var client = await GetOrCreateClientAsync(resourceId);

                // Get deployment in namespace to verify it exists
                var deploy = await client.AppsV1.ReadNamespacedStatefulSetAsync(deployment, _namespace);
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
                var patchResult = await client.AppsV1.PatchNamespacedStatefulSetAsync(
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

            var client = await GetOrCreateClientAsync(resourceId);
            // get pod in namespace
            var podObj = await client.CoreV1.ReadNamespacedPodAsync(pod, _namespace);
            if (podObj == null)
            {
                return "Pod not found";
            }

            if (string.IsNullOrEmpty(containerName))
            {
                containerName = podObj.Spec.Containers.FirstOrDefault()?.Name;
            }

            // get logs of this pod with HTTP messages
            var response = await client.CoreV1.ReadNamespacedPodLogWithHttpMessagesAsync(
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
            var client = await GetOrCreateClientAsync(resourceId);
            var crds = await client.ApiextensionsV1.ListCustomResourceDefinitionAsync();
            var crdInfo = crds.Items.Select(crd =>
            $"{crd.Metadata.Name} (Group: {crd.Spec.Group}, Kind: {crd.Spec.Names.Kind})"
            );

            return string.Join("\n", crdInfo);
        }

        public async Task<string> ListCustomResourcesAsync(string resourceId, string _namespace, string apiGroup, string kind)
        {
            try
            {
                var client = await GetOrCreateClientAsync(resourceId);
                // Get the plural name and version from CRDs
                var crds = await client.ApiextensionsV1.ListCustomResourceDefinitionAsync();
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
                var response = await client.CustomObjects.ListNamespacedCustomObjectWithHttpMessagesAsync(
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
            var prometheusQueryEndpoint = await GetPrometheusEndpoint(AKSClusterResourceId);
            // If we still don't have an endpoint, cannot proceed
            if (string.IsNullOrEmpty(prometheusQueryEndpoint))
            {
                return $"No Prometheus query endpoint available for AKS cluster {AKSClusterResourceId}. Metrics cannot be retrieved. Please confirm if AKS has enabled Azure Monitor and agent has access to it.";
            }

            if (string.IsNullOrEmpty(startTime) || string.IsNullOrEmpty(endTime))
            {
                _logger?.LogInternalInformation(
                    $"Parameter startTime ('{startTime}') or endTime ('{endTime}') is empty for GetKubeResourceMetricsRangeAsync. Defaulting to the last 30 minutes.");
                startTime = DateTime.UtcNow.AddMinutes(-30).ToString("o");
                endTime = DateTime.UtcNow.ToString("o");
            }

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
                    prometheusQueryEndpoint, query);

                // Query the Prometheus endpoint using the injected service
                var response = await _prometheusQueryService.QueryRangeAsync(prometheusQueryEndpoint, query, startDate, endDate, step);

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
                var match = Regex.Match(timeDate, @"(\d+)\s*([smhdwy]|min|minute|hour|day|week|month|year)s?\s*ago",
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

        /// <summary>
        /// Fetch the Azure Monitor Workspace's Prometheus query endpoint that is connected to the specified AKS cluster
        /// </summary>
        /// <param name="aksClusterResourceId">The AKS cluster resource ID</param>
        /// <returns>The Prometheus query endpoint URL if found, null otherwise</returns>
        private async Task<string?> GetPrometheusQueryEndpointFromGraphDb(string aksClusterResourceId)
        {
            try
            {
                _logger?.LogInternalInformation("Looking for Azure Monitor Workspace connected to AKS cluster {ResourceId}", aksClusterResourceId);

                if (_graphDbClient == null)
                {
                    _logger?.LogInternalWarning("Graph database client is not available");
                    return null;
                }

                // Query to find Azure Monitor Workspace nodes that have an edge from the AKS cluster with relationship type "MonitoredBy"
                var query = $@"g.V().has('resourceId', '{aksClusterResourceId.ToLowerInvariant()}').has('isDeleted', false)
                             .out('MONITORED_BY')
                             .hasLabel('microsoft.monitor/accounts').has('isDeleted', false)
                             .has('prometheusQueryEndpoint')
                             .values('prometheusQueryEndpoint')
                             .limit(1)";
                var result = await _graphDbClient.Query<string>(query);

                // Process the result from the Gremlin query
                if (result != null)
                {
                    // Iterate through the result set to find the endpoint
                    foreach (var item in result)
                    {
                        if (item != null)
                        {
                            string prometheusEndpoint = item.ToString();
                            _logger?.LogInternalInformation("Found Prometheus query endpoint {PrometheusEndpoint} for AKS cluster {ResourceId}",
                                prometheusEndpoint, aksClusterResourceId);
                            return prometheusEndpoint;
                        }
                    }
                }

                _logger?.LogInternalInformation("No Prometheus query endpoint found for AKS cluster {ResourceId}", aksClusterResourceId);
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Error retrieving Prometheus query endpoint from graph database: {ErrorMessage}", ex.Message);
                return null;
            }
        }

        public async Task<string> GetKubeResourceEventsAsync(string resourceId, string _namespace, string apiGroup, string kind, string name)
        {
            try
            {
                var client = await GetOrCreateClientAsync(resourceId);
                string uid;

                switch (kind.ToLowerInvariant())
                {
                    case "node":
                        var nodeObj = await client.CoreV1.ReadNodeAsync(name);
                        uid = nodeObj.Metadata.Uid;
                        break;
                    case "deployment":
                        var deployment = await client.AppsV1.ReadNamespacedDeploymentAsync(name, _namespace);
                        uid = deployment.Metadata.Uid;
                        break;
                    case "statefulset":
                        var statefulset = await client.AppsV1.ReadNamespacedStatefulSetAsync(name, _namespace);
                        uid = statefulset.Metadata.Uid;
                        break;
                    case "daemonset":
                        var daemonset = await client.AppsV1.ReadNamespacedDaemonSetAsync(name, _namespace);
                        uid = daemonset.Metadata.Uid;
                        break;
                    case "service":
                        var service = await client.CoreV1.ReadNamespacedServiceAsync(name, _namespace);
                        uid = service.Metadata.Uid;
                        break;
                    case "pod":
                        var pod = await client.CoreV1.ReadNamespacedPodAsync(name, _namespace);
                        uid = pod.Metadata.Uid;
                        break;
                    default:
                        // Handle custom resources
                        try
                        {
                            // Get the plural name and version from CRDs
                            var crds = await client.ApiextensionsV1.ListCustomResourceDefinitionAsync();
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
                            var response = await client.CustomObjects.GetNamespacedCustomObjectAsync(
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

                Corev1EventList events;

                string fieldSelector = !string.IsNullOrEmpty(uid)
                    ? $"involvedObject.uid={uid}"
                    : $"involvedObject.kind={kind},involvedObject.name={name}";

                if (!string.IsNullOrEmpty(_namespace))
                {
                    if (!string.IsNullOrEmpty(uid)) fieldSelector += $",involvedObject.namespace={_namespace}"; // UID is primary, namespace refines
                    else fieldSelector += $",involvedObject.namespace={_namespace}";

                    events = await client.CoreV1.ListNamespacedEventAsync(_namespace, fieldSelector: fieldSelector);
                }
                else // No namespace provided, or for cluster-scoped resources like Node/PV
                {
                    // For cluster-scoped resources, or if _namespace is explicitly null, list events across all namespaces.
                    events = await client.CoreV1.ListEventForAllNamespacesAsync(fieldSelector: fieldSelector);
                }

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

        public async Task<string> GetKubeResourceSpecStatusAsync(string resourceId, string? _namespace, string apiGroup, string kind, string name)
        {
            try
            {
                var client = await GetOrCreateClientAsync(resourceId);
                switch (kind.ToLowerInvariant())
                {
                    case "node":
                        return await GetKubeNodeSpecStatusAsync(resourceId, name);
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
                        var service = await client.CoreV1.ReadNamespacedServiceAsync(name, _namespace);
                        if (service == null)
                        {
                            return "Service not found";
                        }
                        return YamlHelper.Serialize(service);
                    case "configmap":
                        var configMap = await client.CoreV1.ReadNamespacedConfigMapAsync(name, _namespace);
                        if (configMap == null)
                        {
                            return "ConfigMap not found";
                        }
                        return YamlHelper.Serialize(configMap);
                    case "persistentvolume":
                    case "pv":
                        var pv = await client.CoreV1.ReadPersistentVolumeAsync(name);
                        if (pv == null)
                        {
                            return "PV not found";
                        }
                        return YamlHelper.Serialize(pv);
                    case "persistentvolumeclaim":
                    case "pvc":
                        var pvc = await client.CoreV1.ReadNamespacedPersistentVolumeClaimAsync(name, _namespace);
                        if (pvc == null)
                        {
                            return "PVC not found";
                        }
                        return YamlHelper.Serialize(pvc);
                    case "ingress":
                        var ingress = await client.NetworkingV1.ReadNamespacedIngressAsync(name, _namespace);
                        if (ingress == null)
                        {
                            return "Ingress not found";
                        }
                        return YamlHelper.Serialize(ingress);
                    case "cronjob":
                        var cronJob = await client.BatchV1.ReadNamespacedCronJobAsync(name, _namespace);
                        if (cronJob == null)
                        {
                            return "CronJob not found";
                        }
                        return YamlHelper.Serialize(cronJob);
                    case "job":
                        var job = await client.BatchV1.ReadNamespacedJobAsync(name, _namespace);
                        if (job == null)
                        {
                            return "Job not found";
                        }
                        return YamlHelper.Serialize(job);

                }

                // Get the plural name and version from CRDs
                var crds = await client.ApiextensionsV1.ListCustomResourceDefinitionAsync();
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
                var response = await client.CustomObjects.GetNamespacedCustomObjectAsync(
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
        }        /// <summary>
                 /// Fetches metrics from Azure Monitor Prometheus endpoint from graph database or settings.
                 /// </summary>
        private async Task<string> GetAzureMonitorPrometheusMetricsAsync(
            string resourceId,
            string _namespace,
            string workloadType,
            string workloadName,
            string metricType,
            string timeRange)
        {
            // Try to update the Prometheus endpoint from graph database if we don't have one

            var prometheusQueryEndpoint = await GetPrometheusEndpoint(resourceId);
            // If we still don't have an endpoint, cannot proceed
            if (string.IsNullOrEmpty(prometheusQueryEndpoint))
            {
                return $"No Prometheus query endpoint available for AKS cluster {resourceId}. Metrics cannot be retrieved. Please confirm if AKS has enabled Azure Monitor and agent has access to it.";
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
                    prometheusQueryEndpoint, query);

                // Query the Prometheus endpoint using the injected service
                var response = await _prometheusQueryService.QueryInstantAsync(prometheusQueryEndpoint, query);

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
                                var dataPoint = $"{dateTime:yyyy-MM-ddTHH:mm:ss}|{numericValue:F2}|{capitalizedMetricType} Usage %";
                                dataPoints.Add(dataPoint);
                            }
                        }
                    }

                    // Join all data points with semicolons
                    return string.Join(";", dataPoints);
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
        private string[] BuildPromQueries(string metricType, string _namespace, string resourceType, string resourceName, string duration)
        {
            if (resourceType.ToLowerInvariant() == "node")
            {
                switch (metricType.ToLowerInvariant())
                {
                    case "cpu":
                        return new[] {
                $"(1 - avg(rate(node_cpu_seconds_total{{mode=\"idle\", instance=\"{resourceName}\"}}[2m])) by (instance)) * 100",
            };
                    case "memory":
                        return new[] {
                $"(1 - node_memory_MemAvailable_bytes{{instance=\"{resourceName}\"}} / node_memory_MemTotal_bytes{{instance=\"{resourceName}\"}}) * 100",
            };
                    case "availability":
                        return new[] {
                $"(up{{instance=\"{resourceName}\"}}) * 100"
            };
                    default:
                        _logger?.LogInternalWarning(
                            "Unsupported metric type '{MetricType}' for Node '{NodeName}'.",
                            metricType, resourceName);
                        return new[] { $"Unsupported metric type '{metricType}' for Node." };
                }
            }
            else
            {
                var podFilter = "";

                switch (resourceType.ToLowerInvariant())
                {
                    case "deployment":
                        podFilter = $"^{resourceName}-(?:[a-z0-9]+)(?:-[a-z0-9]+)?$";
                        break;
                    case "replicaset":
                        podFilter = $"^{resourceName}-(?:[a-z0-9]+)$";
                        break;
                    case "daemonset":
                        podFilter = $"^{resourceName}-(?:[a-z0-9]+)$";
                        break;
                    case "statefulset":
                        podFilter = $"^{resourceName}-(?:[a-z0-9]+)$";
                        break;
                    default:
                        podFilter = $"^{resourceName}$";
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
                            "No query configured for metric type '{MetricType}' in namespace '{Namespace}', workload type '{resourceType}', and workload name '{resourceName}'.",
                            metricType, _namespace, resourceType, resourceName);

                        return new[] { $"No query configured for metric type '{metricType}' in namespace '{_namespace}', workload type '{resourceType}', and workload name '{resourceName}'." };

                }
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

        public async Task<string> ListWorkloadRevisions(string resourceId, string _namespace, string kind, string name)
        {
            var client = await GetOrCreateClientAsync(resourceId);
            var sb = new StringBuilder();

            try
            {
                switch (kind.ToLowerInvariant())
                {
                    case "deployment":
                        // For deployments, we need to get all ReplicaSets and filter those owned by our deployment
                        var deployment = await client.AppsV1.ReadNamespacedDeploymentAsync(name, _namespace);
                        if (deployment == null)
                        {
                            return $"Deployment '{name}' not found in namespace '{_namespace}'";
                        }

                        var replicaSets = await client.AppsV1.ListNamespacedReplicaSetAsync(
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
                        var statefulSet = await client.AppsV1.ReadNamespacedStatefulSetAsync(name, _namespace);
                        if (statefulSet == null)
                        {
                            return $"StatefulSet '{name}' not found in namespace '{_namespace}'";
                        }

                        var controllerRevisions = await client.AppsV1.ListNamespacedControllerRevisionAsync(
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

        /// <summary>
        /// Updates the prometheusQueryEndpoint for a given AKS cluster resource ID if it's not already set
        /// </summary>
        /// <param name="aksClusterResourceId">AKS cluster resource ID</param>
        /// <returns>True if the endpoint was found and updated, false otherwise</returns>
        private async Task<string> GetPrometheusEndpoint(string aksClusterResourceId)
        {
            if (_graphDbClient == null)
            {
                _logger?.LogInternalWarning("Graph database client is not available to update Prometheus query endpoint");
                return "";
            }

            try
            {
                // Get the Prometheus endpoint from the graph database
                string? prometheusEndpoint = await GetPrometheusQueryEndpointFromGraphDb(aksClusterResourceId);
                return prometheusEndpoint;

            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Error updating Prometheus query endpoint from graph database: {ErrorMessage}", ex.Message);
                return "";
            }
        }

        private string LoadScriptContent(string scriptFileName)
        {
            var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "KubernetesAgent", scriptFileName);

            if (!File.Exists(scriptPath))
            {
                _logger?.LogError("Script file not found at: {ScriptPath}", scriptPath);
                throw new FileNotFoundException($"Script '{scriptFileName}' not found at '{scriptPath}'. Ensure it's copied to output.", scriptPath);
            }
            _logger?.LogDebug("Loading script from: {ScriptPath}", scriptPath);
            return File.ReadAllText(scriptPath);
        }

        public async Task<string> ProfileDotnetAppCpuInAKSContainerAsync(
            string aksResourceId,
            string _namespace,
            string podName,
            string? targetContainerName,
            int durationSeconds = 30)
        {
            var client = await GetOrCreateClientAsync(aksResourceId);
            if (client == null) // Should not happen if GetOrCreateClientAsync is correct
            {
                return "Error: Kubernetes client could not be initialized.";
            }

            V1Pod pod;
            try
            {
                pod = await client.CoreV1.ReadNamespacedPodAsync(podName, _namespace);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error reading pod '{PodName}' in namespace '{Namespace}' for AKS resource '{AksResourceId}'.", podName, _namespace, aksResourceId);
                return $"Error reading pod '{podName}': {ex.Message}";
            }

            if (pod == null) // Should be caught by HttpOperationException, but as a safeguard
            {
                _logger?.LogWarning("Pod object was null after read for '{PodName}' in namespace '{Namespace}'.", podName, _namespace);
                return $"Pod '{podName}' not found in namespace '{_namespace}' (object was null after read).";
            }


            // Determine target container
            if (string.IsNullOrEmpty(targetContainerName))
            {
                if (pod.Spec.Containers.Any()) // Check if there are any containers
                {
                    targetContainerName = pod.Spec.Containers[0].Name; // Always pick the first container
                }
                else
                {
                    _logger?.LogError("Pod '{PodName}' in namespace '{Namespace}' has no containers defined.", podName, _namespace);
                    return $"Pod '{podName}' has no containers.";
                }
                _logger?.LogInformation("Auto-selected container '{SelectedContainer}' for profiling in pod '{PodName}'.", targetContainerName, podName);
            }
            else if (!pod.Spec.Containers.Any(c => c.Name.Equals(targetContainerName, StringComparison.OrdinalIgnoreCase)))
            {
                _logger?.LogWarning("Specified target container '{TargetContainerName}' not found in pod '{PodName}'. Available: {AvailableContainers}",
                                    targetContainerName, podName, string.Join(", ", pod.Spec.Containers.Select(c => c.Name)));
                return $"Container '{targetContainerName}' not found in pod '{podName}'. Available: {string.Join(", ", pod.Spec.Containers.Select(c => c.Name))}";
            }

            _logger?.LogInformation("Targeting pod '{PodName}', container '{ContainerName}' for in-container .NET CPU profiling for {Duration} seconds.", podName, targetContainerName, durationSeconds);

            string scriptContent;
            try
            {
                scriptContent = LoadScriptContent("profile_script.sh");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load profiling script for pod '{PodName}'.", podName);
                return $"Error: Could not load profiling script. {ex.Message}";
            }

            var (stdout, stderr, exitCode) = await ExecInPodAsync(
                client, // Pass the initialized client
                _namespace,
                podName,
                targetContainerName,
                "sh", // Command to run the script
                "-c", // Argument to sh: execute the following string
                scriptContent, // The actual script content
                "profiling_script.sh", // $0 argument for the script (its "name")
                durationSeconds.ToString() // $1 argument for the script (duration)
                );

            const string noDotNetProcessMarker = "[PROF_SCRIPT_INFO] No debuggable .NET process found."; // Partial match is fine
            if (stdout != null && stdout.Contains(noDotNetProcessMarker))
            {
                _logger?.LogInformation("In-container script indicated no debuggable .NET process found for pod '{PodName}', container '{ContainerName}', ns '{Namespace}', AKS '{AksResourceId}'. Script stdout contains relevant info.",
                                        podName, targetContainerName, _namespace, aksResourceId);
                // Find the marker and return a concise message including the script's own explanation.
                int markerIndex = stdout.IndexOf(noDotNetProcessMarker);
                string scriptInfoMessage = stdout.Substring(markerIndex).Split('\n')[0].Trim(); // Get the marker line

                return $"CPU profiling was not performed for pod '{podName}', container '{targetContainerName}'. The script reported: \"{scriptInfoMessage}\". This suggests the application may not be a .NET application, or no running .NET process suitable for profiling was found.";
            }

            var resultBuilder = new StringBuilder();
            resultBuilder.AppendLine($"In-Container CPU Profiling Result for Pod: {podName}, Container: {targetContainerName}");
            resultBuilder.AppendLine($"Script Execution Exit Code: {exitCode}");

            bool analysisHeaderFound = false;
            if (!string.IsNullOrEmpty(stdout))
            {
                resultBuilder.AppendLine("--- Script Standard Output ---");
                string[] lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("ANALYSIS START", StringComparison.Ordinal)) analysisHeaderFound = true;
                    resultBuilder.AppendLine(line);
                }
            }

            if (!string.IsNullOrEmpty(stderr))
            {
                // Filter known benign warnings from apt-get to reduce noise for the user, but log them.
                string filteredStderr = stderr;
                string[] benignWarnings = {
                    "dpkg-preconfigure: unable to re-open stdin: No such file or directory",
                    "debconf: delaying package configuration, since apt-utils is not installed",
                    "Non-interactive DEBIAN_FRONTEND"
                };
                foreach (var warning in benignWarnings)
                {
                    if (stderr.Contains(warning, StringComparison.OrdinalIgnoreCase))
                    {
                        // Log the full stderr if it contained a benign warning, then filter for user output
                        _logger?.LogDebug("Benign warning pattern '{WarningPattern}' found in stderr for pod '{PodName}'. Full stderr:\n{FullStderr}", warning, podName, stderr);
                        // This replacement can be tricky if warnings are multi-line or have variations.
                        // For simplicity, just a basic replace. More robust filtering might be needed.
                        filteredStderr = Regex.Replace(filteredStderr, Regex.Escape(warning) + @"\s*\n?", "", RegexOptions.IgnoreCase);
                    }
                }
                filteredStderr = filteredStderr.Trim(); // Trim whitespace after potential replacements
                if (!string.IsNullOrWhiteSpace(filteredStderr))
                {
                    resultBuilder.AppendLine("--- Script Standard Error ---");
                    resultBuilder.AppendLine(filteredStderr);
                }
            }

            if (exitCode != 0)
            {
                _logger?.LogError("In-container profiling script failed for pod '{PodName}'. ExitCode: {ExitCode}. Full Stdout:\n{FullStdout}\nFull Stderr:\n{FullStderr}",
                                 podName, exitCode, stdout, stderr);
                if (!analysisHeaderFound && !resultBuilder.ToString().Contains("ERROR: Profiling script encountered an error"))
                {
                    // Add a generic error message if not already present from script's stdout/stderr processing
                    resultBuilder.AppendLine("ERROR: Profiling script failed. Review script output and error streams for details.");
                }
            }
            else if (!analysisHeaderFound)
            {
                _logger?.LogWarning("In-container profiling script completed with exit code 0 for pod '{PodName}', but analysis header was not found in stdout. Full Stdout:\n{FullStdout}", podName, stdout);
                resultBuilder.AppendLine("WARNING: Script completed but analysis output marker was not found. Check script standard output.");
            }
            else
            {
                _logger?.LogInformation("In-container profiling script completed successfully for pod '{PodName}'. Analysis should be in the output.", podName);
            }

            return resultBuilder.ToString();
        }

        private async Task<(string stdout, string stderr, int exitCode)> ExecInPodAsync(
            IKubernetes client,
            string ns,
            string podName,
            string containerName,
            string command, // This will be "sh"
            params string[] args) // This will be ["-c", "actual_script_content_here", "script_name_for_0_arg", "duration_arg"]
        {
            var commandList = new List<string> { command };
            commandList.AddRange(args);

            _logger?.LogInformation("ExecInPodAsync: Pod: '{PodName}', Container: '{ContainerName}', Namespace: '{Namespace}', Command: '{FullCommand}'",
                                    podName, containerName, ns, string.Join(" ", commandList));

            System.Net.WebSockets.WebSocket? webSocket = null;
            StreamDemuxer? demux = null;
            MemoryStream? stdoutMemoryStream = null;
            MemoryStream? stderrMemoryStream = null;

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            try
            {
                webSocket = await client.WebSocketNamespacedPodExecAsync(
                    podName,
                    ns,
                    commandList,
                    containerName,
                    stdin: false,
                    stdout: true,
                    stderr: true,
                    tty: false,
                    cancellationToken: cts.Token).ConfigureAwait(false);

                demux = new StreamDemuxer(webSocket); // Uses default buffer size
                demux.Start();

                stdoutMemoryStream = new MemoryStream();
                stderrMemoryStream = new MemoryStream();

                // ChannelIndex.StdOutByte is 1, ChannelIndex.StdErrByte is 2. Stream ID 0 for main stream.
                Stream stdoutStreamFromDemux = demux.GetStream((byte)ChannelIndex.StdOut, 0);
                Stream stderrStreamFromDemux = demux.GetStream((byte)ChannelIndex.StdErr, 0);

                var copyStdOutTask = Task.CompletedTask;
                if (stdoutStreamFromDemux != null) // StreamDemuxer GetStream can return null
                {
                    copyStdOutTask = stdoutStreamFromDemux.CopyToAsync(stdoutMemoryStream, 81920, cts.Token);
                }
                else
                {
                    _logger?.LogWarning("ExecInPodAsync: Stdout stream from demuxer was null for pod '{PodName}'.", podName);
                }

                var copyStdErrTask = Task.CompletedTask;
                if (stderrStreamFromDemux != null)
                {
                    copyStdErrTask = stderrStreamFromDemux.CopyToAsync(stderrMemoryStream, 81920, cts.Token);
                }
                else
                {
                    _logger?.LogWarning("ExecInPodAsync: Stderr stream from demuxer was null for pod '{PodName}'.", podName);
                }

                await Task.WhenAll(copyStdOutTask, copyStdErrTask).ConfigureAwait(false);

                string stdoutResult = stdoutMemoryStream != null ? Encoding.UTF8.GetString(stdoutMemoryStream.ToArray()) : string.Empty;
                string stderrResult = stderrMemoryStream != null ? Encoding.UTF8.GetString(stderrMemoryStream.ToArray()) : string.Empty;

                int exitCode = 0;
                if (!string.IsNullOrEmpty(stderrResult))
                {
                    exitCode = 1;
                    try
                    {
                        var status = KubernetesJson.Deserialize<V1Status>(stderrResult);
                        if (status != null && status.Reason == "NonZeroExitCode" && status.Details?.Causes != null)
                        {
                            var cause = status.Details.Causes.FirstOrDefault(c => c.Reason == "ExitCode");
                            if (cause != null && int.TryParse(cause.Message, out int ec))
                            {
                                exitCode = ec;
                                _logger?.LogInformation("ExecInPodAsync: Parsed exit code {ExitCode} from V1Status in stderr for pod '{PodName}'.", exitCode, podName);
                            }
                        }
                        else if (status != null && (status.Status == "Failure" || !string.IsNullOrEmpty(status.Reason)))
                        {
                            _logger?.LogWarning("ExecInPodAsync: V1Status in stderr indicates failure for pod '{PodName}': {StatusMessage}", podName, status.Message);
                        }
                    }
                    catch (JsonException) { /* Not a V1Status, just regular stderr. */ }
                }
                if (exitCode == 0 && stdoutResult.Contains("[PROF_SCRIPT] ERROR:"))
                {
                    exitCode = 1;
                }

                _logger?.LogDebug("ExecInPodAsync: Stdout for pod '{PodName}':\n{Stdout}", podName, stdoutResult);
                if (!string.IsNullOrEmpty(stderrResult))
                {
                    if (exitCode != 0) _logger?.LogWarning("ExecInPodAsync: Stderr for pod '{PodName}':\n{Stderr}", podName, stderrResult);
                    else _logger?.LogDebug("ExecInPodAsync: Stderr (exit code 0) for pod '{PodName}':\n{Stderr}", podName, stderrResult);
                }

                return (stdoutResult, stderrResult, exitCode);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                _logger?.LogError("ExecInPodAsync: Operation timed out for pod '{PodName}'.", podName);
                return (stdoutMemoryStream != null ? Encoding.UTF8.GetString(stdoutMemoryStream.ToArray()) : string.Empty,
                        (stderrMemoryStream != null ? Encoding.UTF8.GetString(stderrMemoryStream.ToArray()) : string.Empty) + "\n[EXEC_ERROR] Operation timed out.",
                        -1);
            }
            catch (KubernetesException ex)
            {
                _logger?.LogError(ex, "ExecInPodAsync: KubernetesException for pod '{PodName}'.", podName);
                return (string.Empty, $"[EXEC_K8S_ERROR] {ex.Message}", -1);
            }
            catch (System.Net.WebSockets.WebSocketException ex)
            {
                _logger?.LogError(ex, "ExecInPodAsync: WebSocketException for pod '{PodName}'. Error Code: {ErrorCode}.", podName, ex.WebSocketErrorCode);
                return (string.Empty, $"[EXEC_WEBSOCKET_ERROR] {ex.Message}", ex.NativeErrorCode != 0 ? ex.NativeErrorCode : -1);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ExecInPodAsync: Unexpected exception for pod '{PodName}'.", podName);
                return (stdoutMemoryStream != null ? Encoding.UTF8.GetString(stdoutMemoryStream.ToArray()) : string.Empty,
                        (stderrMemoryStream != null ? Encoding.UTF8.GetString(stderrMemoryStream.ToArray()) : string.Empty) + $"\n[EXEC_UNEXPECTED_ERROR] {ex.Message}",
                        -1);
            }
            finally
            {
                stdoutMemoryStream?.Dispose();
                stderrMemoryStream?.Dispose();
                if (demux != null)
                {
                    demux.Dispose(); // This should close the WebSocket
                }
                else if (webSocket != null)
                {
                    if (webSocket.State == System.Net.WebSockets.WebSocketState.Open ||
                        webSocket.State == System.Net.WebSockets.WebSocketState.CloseReceived ||
                        webSocket.State == System.Net.WebSockets.WebSocketState.CloseSent)
                    {
                        try
                        {
                            using var closeWebSocketCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                            await webSocket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Client closing", closeWebSocketCts.Token).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "ExecInPodAsync: Exception during WebSocket explicit close for pod '{PodName}'. State: {WebSocketState}", podName, webSocket.State);
                        }
                    }
                    webSocket.Dispose();
                }
            }
        }

        public async Task<string> AnalyzeDotnetAppMemoryInAKSContainerAsync(
            string aksResourceId,
            string _namespace,
            string podName,
            string? targetContainerName)
        {
            var client = await GetOrCreateClientAsync(aksResourceId);
            if (client == null)
            {
                return "Error: Kubernetes client could not be initialized.";
            }

            V1Pod pod;
            try
            {
                pod = await client.CoreV1.ReadNamespacedPodAsync(podName, _namespace);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error reading pod '{PodName}' in namespace '{Namespace}' for memory analysis.", podName, _namespace);
                return $"Error reading pod '{podName}': {ex.Message}";
            }

            // Determine target container (same logic as ProfileDotnetAppCpuInAKSContainerAsync)
            if (string.IsNullOrEmpty(targetContainerName))
            {
                targetContainerName = pod.Spec.Containers.FirstOrDefault()?.Name;
                if (string.IsNullOrEmpty(targetContainerName))
                {
                    _logger?.LogError("Pod '{PodName}' in namespace '{Namespace}' has no containers defined for memory analysis.", podName, _namespace);
                    return $"Error: Pod '{podName}' has no containers.";
                }
                _logger?.LogInformation("Auto-selected container '{SelectedContainer}' for memory analysis in pod '{PodName}'.", targetContainerName, podName);
            }
            else if (!pod.Spec.Containers.Any(c => c.Name.Equals(targetContainerName, StringComparison.OrdinalIgnoreCase)))
            {
                _logger?.LogWarning("Specified target container '{TargetContainerName}' not found in pod '{PodName}' for memory analysis. Available: {AvailableContainers}",
                                    targetContainerName, podName, string.Join(", ", pod.Spec.Containers.Select(c => c.Name)));
                return $"Error: Container '{targetContainerName}' not found in pod '{podName}'. Available: {string.Join(", ", pod.Spec.Containers.Select(c => c.Name))}";
            }

            _logger?.LogInformation("Starting in-container .NET memory analysis for pod '{PodName}', container '{ContainerName}'.", podName, targetContainerName);

            string scriptContent;
            try
            {
                scriptContent = LoadScriptContent("analyze_memory_aks.sh");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load memory analysis script for pod '{PodName}'.", podName);
                return $"Error: Could not load memory analysis script. {ex.Message}";
            }

            var (stdout, stderr, exitCode) = await ExecInPodAsync(
                client,
                _namespace,
                podName,
                targetContainerName,
                "sh",
                "-c",
                scriptContent
            );

            var resultBuilder = new StringBuilder();
            resultBuilder.AppendLine($"In-Container .NET Memory Analysis Result for Pod: {podName}, Container: {targetContainerName}");
            resultBuilder.AppendLine($"Script Execution Exit Code: {exitCode}");

            const string noDotNetProcessMarker = "[MEM_ANALYSIS_SCRIPT_INFO] No debuggable .NET process found.";
            if (stdout != null && stdout.Contains(noDotNetProcessMarker))
            {
                _logger?.LogInformation("Memory analysis script indicated no debuggable .NET process found for pod '{PodName}'.", podName);
                resultBuilder.AppendLine("--- Script Standard Output ---");
                resultBuilder.AppendLine(stdout);
                if (!string.IsNullOrEmpty(stderr))
                {
                    resultBuilder.AppendLine("--- Script Standard Error ---");
                    resultBuilder.AppendLine(stderr);
                }
                // Extract the specific marker line for a concise message
                int markerIndex = stdout.IndexOf(noDotNetProcessMarker);
                string scriptInfoMessage = stdout.Substring(markerIndex).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? noDotNetProcessMarker;
                return $"Memory analysis not performed: \"{scriptInfoMessage.Trim()}\". This suggests the application may not be a .NET application, or no running .NET process suitable for analysis was found. Full script output:\n{resultBuilder.ToString()}";
            }

            const string analysisStartMarker = "-------------------- ANALYSIS START --------------------";
            const string analysisEndMarker = "-------------------- ANALYSIS END ----------------------";

            if (!string.IsNullOrEmpty(stdout))
            {
                int startIndex = stdout.IndexOf(analysisStartMarker);
                int endIndex = stdout.IndexOf(analysisEndMarker, startIndex + analysisStartMarker.Length);

                if (startIndex != -1 && endIndex != -1)
                {
                    startIndex += analysisStartMarker.Length;
                    string analysisResult = stdout.Substring(startIndex, endIndex - startIndex).Trim();
                    resultBuilder.AppendLine("--- Memory Analysis ---");
                    resultBuilder.AppendLine(analysisResult);
                    _logger?.LogInformation("Memory analysis script completed with analysis data for pod '{PodName}'.", podName);
                }
                else
                {
                    resultBuilder.AppendLine("--- Script Standard Output (Analysis markers not found or incomplete) ---");
                    resultBuilder.AppendLine(stdout);
                    _logger?.LogWarning("Memory analysis script completed for pod '{PodName}', but analysis markers were not found in stdout. Full stdout logged.", podName);
                }
            }
            else
            {
                resultBuilder.AppendLine("--- Script Standard Output (Empty) ---");
            }


            if (!string.IsNullOrEmpty(stderr))
            {
                // Filter known benign warnings (similar to CPU script)
                string filteredStderr = stderr;
                // Add any memory-analysis specific benign warnings if they appear
                // ...
                filteredStderr = filteredStderr.Trim();
                if (!string.IsNullOrWhiteSpace(filteredStderr))
                {
                    resultBuilder.AppendLine("--- Script Standard Error ---");
                    resultBuilder.AppendLine(filteredStderr);
                }
            }

            if (exitCode != 0 && !stdout.Contains(analysisStartMarker)) // If failed and no analysis was even started by script
            {
                _logger?.LogError("Memory analysis script failed for pod '{PodName}'. ExitCode: {ExitCode}. Full Stdout:\n{FullStdout}\nFull Stderr:\n{FullStderr}",
                                    podName, exitCode, stdout, stderr);
                if (!resultBuilder.ToString().Contains("ERROR: Memory analysis script encountered an error"))
                {
                    resultBuilder.AppendLine("ERROR: Memory analysis script failed before producing analysis. Review script output and error streams for details.");
                }
            }

            return resultBuilder.ToString();
        }

        public async Task<string> DiscoverMetricsAsync(
            string AKSClusterResourceId,
            string? namePattern,
            string? metricType)
        {
            var prometheusQueryEndpoint = await GetPrometheusEndpoint(AKSClusterResourceId);
            // If we still don't have an endpoint, cannot proceed
            if (string.IsNullOrEmpty(prometheusQueryEndpoint))
            {
                return $"No Prometheus query endpoint available for AKS cluster {AKSClusterResourceId}. Metrics cannot be retrieved. Please confirm if AKS has enabled Azure Monitor and agent has access to it.";
            }

            return await _prometheusQueryService.DiscoverMetricsAsync(prometheusQueryEndpoint, namePattern, metricType);
        }

        public async Task<string> GetMetricLabelsAsync(
            string AKSClusterResourceId,
            string metricName,
            string? labelName)
        {
            var prometheusQueryEndpoint = await GetPrometheusEndpoint(AKSClusterResourceId);
            // If we still don't have an endpoint, cannot proceed
            if (string.IsNullOrEmpty(prometheusQueryEndpoint))
            {
                return $"No Prometheus query endpoint available for AKS cluster {AKSClusterResourceId}. Metrics cannot be retrieved. Please confirm if AKS has enabled Azure Monitor and agent has access to it.";
            }

            return await _prometheusQueryService.GetMetricLabelsAsync(prometheusQueryEndpoint, metricName, labelName);
        }

        public async Task<string> ExecutePromQLAsync(
            string AKSClusterResourceId,
            string query,
            string duration,
            string step,
            string? labelFilters,
            string? aggregateFunction,
            string? aggregateBy,
            int? limit,
            double? minValue)
        {
            var prometheusQueryEndpoint = await GetPrometheusEndpoint(AKSClusterResourceId);
            // If we still don't have an endpoint, cannot proceed
            if (string.IsNullOrEmpty(prometheusQueryEndpoint))
            {
                return $"No Prometheus query endpoint available for AKS cluster {AKSClusterResourceId}. Metrics cannot be retrieved. Please confirm if AKS has enabled Azure Monitor and agent has access to it.";
            }

            return await _prometheusQueryService.ExecutePromQLAsync(
                prometheusQueryEndpoint,
                query,
                duration,
                step,
                labelFilters,
                aggregateFunction,
                aggregateBy,
                limit,
                minValue);
        }
    }
}
