// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using ScottPlot;
using Agent.Core.Helpers;
using Agent.Core.Models.Charts;
using Agent.Core.Models;
using k8s;
using k8s.Models;
using Agent.Core.Configuration;
using System.Text.Json;
using Newtonsoft.Json;
using YamlDotNet.Serialization;
using Newtonsoft.Json.Converters;
using System.Dynamic;
using System.Text;
using System.Collections.Concurrent;
using Azure.ResourceManager;
using Azure.Identity;
using Azure.Core;
using Azure.ResourceManager.ContainerService;

namespace Agent.Plugins
{
    public class KubePlugin : IKubePlugin
    {
        private readonly ILogger? _logger;
        private IKubernetes _client;
        private readonly ConcurrentDictionary<string, IKubernetes> _clientCache = new();
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(60);
        private readonly ConcurrentDictionary<string, DateTimeOffset> _cacheTimestamps = new();
        private string kubeConfigPath;

        public KubePlugin(IConfiguration configuration, ILogger<KubePlugin>? logger)
        {
            _logger = logger;
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
            var credential = new DefaultAzureCredential();
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

        // get pods of a deployment in a namespace
        public async Task<string> GetKubePodsAsync(string resourceId, string _namespace, string deployment)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            // get deployment in namespace
            var deploy = await _client.AppsV1.ReadNamespacedDeploymentAsync(deployment, _namespace);
            if (deploy == null)
            {
                return "Deployment not found";
            }
            // extract pod spec labels in the deployment
            var labels = deploy.Spec.Template.Metadata.Labels;
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

            // get logs of this pod with HTTP messages
            var response = await _client.CoreV1.ReadNamespacedPodLogWithHttpMessagesAsync(pod, _namespace, tailLines: lines);

            if (response.Response.Content == null)
            {
                return string.Empty;
            }

            // read the stream to string
            using var reader = new StreamReader(await response.Response.Content.ReadAsStreamAsync());
            return await reader.ReadToEndAsync();
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

        // show the resource usage of all pod in a namespace with percentage
        public async Task<string> ListKubePodResourceUsageByNamespaceAsync(string resourceId, string _namespace)
        {
            _client = await GetOrCreateClientAsync(resourceId);
            // Get all pods in namespace to get their resource limits
            var pods = await _client.CoreV1.ListNamespacedPodAsync(_namespace);
            // Get resource usage metrics
            var podMetrics = await _client.GetKubernetesPodsMetricsByNamespaceAsync(_namespace);
            if (podMetrics == null)
            {
                return "Pod metrics not found";
            }

            var usageList = new List<string>();
            foreach (var pod in podMetrics.Items)
            {
                var podSpec = pods.Items.FirstOrDefault(p => p.Metadata.Name == pod.Metadata.Name);
                if (podSpec == null) continue;

                var cpuUsage = 0m;
                var memoryUsage = 0m;
                var cpuLimit = 0m;
                var memoryLimit = 0m;

                // Sum up usage and limits for all containers
                for (int i = 0; i < pod.Containers.Count; i++)
                {

                    var container = pod.Containers[i];
                    var containerSpec = podSpec.Spec.Containers[i];
                    // print following values for troubleshooting
                    Console.WriteLine(
                        "Pod: {0}, Container: {1}, " +
                        "CPU Usage: {2}, Memory Usage: {3}, " +
                        "CPU Limit: {4}, Memory Limit: {5}",
                        pod.Metadata.Name,
                        container.Name,
                        container.Usage.ContainsKey("cpu") ? container.Usage["cpu"].Value : "N/A",
                        container.Usage.ContainsKey("memory") ? container.Usage["memory"].Value : "N/A",
                        containerSpec.Resources.Limits?.ContainsKey("cpu") == true ? containerSpec.Resources.Limits["cpu"].ToString() : "N/A",
                        containerSpec.Resources.Limits?.ContainsKey("memory") == true ? containerSpec.Resources.Limits["memory"].ToString() : "N/A");


                    Console.WriteLine("Metrics: " + string.Join(", ", container.Usage.Select(kv => $"{kv.Key}={kv.Value}")));

                    if (container.Usage.ContainsKey("cpu"))
                    {
                        cpuUsage += ParseCpuValue(container.Usage["cpu"].Value);
                    }
                    if (container.Usage.ContainsKey("memory"))
                    {
                        memoryUsage += ParseMemoryValue(container.Usage["memory"].Value);
                    }

                    // Get CPU limit in millicores
                    if (containerSpec.Resources.Limits?.ContainsKey("cpu") == true)
                    {
                        cpuLimit += ParseCpuValue(containerSpec.Resources.Limits["cpu"].ToString());
                    }

                    // Get memory limit in Ki
                    if (containerSpec.Resources.Limits?.ContainsKey("memory") == true)
                    {
                        memoryLimit += ParseMemoryValue(containerSpec.Resources.Limits["memory"].ToString());
                    }
                }

                var cpuPercentage = cpuLimit > 0 ? (cpuUsage / cpuLimit * 100).ToString("F1") : "-";
                var memoryPercentage = memoryLimit > 0 ? (memoryUsage / memoryLimit * 100).ToString("F1") : "-";

                // TODO: give a more user frendly output
                usageList.Add($"Pod {pod.Metadata.Name}: CPU={cpuUsage}m/{cpuLimit}m ({cpuPercentage}%), Memory={memoryUsage}Mi/{memoryLimit}Mi ({memoryPercentage}%)");
            }

            return string.Join("\n", usageList);
        }

        private decimal ParseCpuValue(string value)
        {
            if (value.EndsWith("m"))
            {
                return decimal.Parse(value.TrimEnd('m'));
            }
            if (value.EndsWith("n"))
            {
                return decimal.Parse(value.TrimEnd('n')) / 1000000; // Convert nanocores to millicores
            }
            return decimal.Parse(value) * 1000; // Convert core to millicores
        }

        private decimal ParseMemoryValue(string value)
        {
            if (value.EndsWith("Ki"))
                return decimal.Parse(value.TrimEnd('K', 'i')) / 1024;
            if (value.EndsWith("Mi"))
                return decimal.Parse(value.TrimEnd('M', 'i'));
            if (value.EndsWith("Gi"))
                return decimal.Parse(value.TrimEnd('G', 'i')) * 1024;
            if (value.EndsWith("Ti"))
                return decimal.Parse(value.TrimEnd('T', 'i')) * 1024 * 1024;
            return decimal.Parse(value);
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
    }
}