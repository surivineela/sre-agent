using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Agent.Plugins.Mocks;
public class MockKubePlugin : IKubePlugin
{
    public string AksClusterResourceId { get; set; } = string.Empty;

    // Add dictionaries for mock data
    private Dictionary<string, string> _mockNamespaces = new Dictionary<string, string>();
    private Dictionary<string, string> _mockDeployments = new Dictionary<string, string>();
    private Dictionary<string, string> _mockStatefulSets = new Dictionary<string, string>();
    private Dictionary<string, Dictionary<string, List<string>>> _deploymentToPods = new Dictionary<string, Dictionary<string, List<string>>>();

    public MockKubePlugin()
    {
    }

    // Add configuration methods
    public void ConfigureNamespaces(string resourceId, string namespacesData)
    {
        _mockNamespaces[resourceId] = namespacesData;
    }

    public void ConfigureDeployments(string resourceId, string _namespace, string deploymentsData)
    {
        var key = $"{resourceId}:{_namespace}";
        _mockDeployments[key] = deploymentsData;

        // Generate pods for each deployment
        var deployments = deploymentsData.Split(',').Select(d => d.Trim()).ToList();
        var random = new Random();

        if (!_deploymentToPods.ContainsKey(key))
        {
            _deploymentToPods[key] = new Dictionary<string, List<string>>();
        }

        foreach (var deployment in deployments)
        {
            // Generate 1-3 pods per deployment
            int podCount = random.Next(1, 4);
            var pods = new List<string>();

            for (int i = 0; i < podCount; i++)
            {
                string randomHash1 = GenerateRandomHash(8);
                string randomHash2 = GenerateRandomHash(5);
                string podName = $"{deployment}-{randomHash1}-{randomHash2}";
                pods.Add(podName);
            }

            _deploymentToPods[key][deployment] = pods;
        }
    }

    public void ConfigureStatefulSets(string resourceId, string _namespace, string statefulSetsData)
    {
        var key = $"{resourceId}:{_namespace}";
        _mockStatefulSets[key] = statefulSetsData;
    }

    public Task<string> ExecCommandInPodAsync(string resourceId, string _namespace, string pod, string? container, string command)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetPodCpuMetricsForWorkloadAsync(string resourceId, string _namespace, string workloadType, string workloadName, string timeRange = "5m")
    {
        throw new NotImplementedException();
    }

    public Task<string> GetKubeResourceSpecStatusAsync(string resourceId, string _namespace, string apiGroup, string kind, string name)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetKubeDeploymentEventsAsync(string resourceId, string _namespace, string deployment)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetKubeDeploymentsAsync(string resourceId, string _namespace)
    {
        var key = $"{resourceId}:{_namespace}";
        if (_mockDeployments.TryGetValue(key, out var deployments))
        {
            return Task.FromResult(deployments);
        }
        return Task.FromResult("No deployments configured for this resource ID and namespace.");
    }

    public Task<string> GetKubeDeploymentSpecStatusAsync(string resourceId, string _namespace, string deployment)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetAKSClusterResourceIdAsync(string subscription, string resourceGroupName, string aksClusterName)
    {
        AksClusterResourceId = $"/subscriptions/{subscription}/resourceGroups/{resourceGroupName}/providers/Microsoft.ContainerService/managedClusters/{aksClusterName}";
        return Task.FromResult($"AKSClusterResourceID is **'/subscriptions/{subscription}/resourceGroups/{resourceGroupName}/providers/Microsoft.ContainerService/managedClusters/{aksClusterName}'**");
    }

    public Task<string> GetKubeNamespacesAsync(string resourceId)
    {
        if (_mockNamespaces.TryGetValue(resourceId, out var namespaces))
        {
            return Task.FromResult(namespaces);
        }
        return Task.FromResult("No namespaces configured for this resource ID.");
    }

    public Task<string> GetKubePodEventsAsync(string resourceId, string _namespace, string pod)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetKubePodLogsAsync(string resourceId, string _namespace, string pod, string container = "", int lines = 100)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetKubePodsAsync(string resourceId, string _namespace, string kind, string deployment)
    {
        var key = $"{resourceId}:{_namespace}";

        // Check if namespace+resource exists and contains the deployment
        if (_mockDeployments.TryGetValue(key, out var deploymentsStr))
        {
            var configuredDeployments = deploymentsStr.Split(',').Select(d => d.Trim()).ToList();

            // Check if the requested deployment is configured
            if (configuredDeployments.Contains(deployment))
            {
                // Check if we have pods generated for this deployment
                if (_deploymentToPods.TryGetValue(key, out var deploymentPods) &&
                    deploymentPods.TryGetValue(deployment, out var pods))
                {
                    return Task.FromResult(string.Join(", ", pods));
                }

                // If somehow we don't have pods generated, return empty
                return Task.FromResult("No pods found for this deployment.");
            }
        }

        return Task.FromResult($"Deployment '{deployment}' not found in namespace '{_namespace}'.");
    }

    private string GenerateRandomHash(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    public Task<string> GetPodMemoryMetricsForWorkloadAsync(string resourceId, string _namespace, string workloadType, string workloadName, string timeRange = "5m")
    {
        throw new NotImplementedException();
    }

    public Task<string> GetPodYamlAsync(string resourceId, string _namespace, string pod)
    {
        throw new NotImplementedException();
    }

    public Task<string> ListCRDsAsync(string resourceId)
    {
        throw new NotImplementedException();
    }

    public Task<string> ListCustomResourcesAsync(string resourceId, string _namespace, string apiGroup, string kind)
    {
        throw new NotImplementedException();
    }

    public Task<string> RolloutRestartDeploymentAsync(string resourceId, string _namespace, string deployment)
    {
        throw new NotImplementedException();
    }

    public Task<string> ScaleDeploymentAsync(string resourceId, string _namespace, string deployment, int replicas)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetRecentlyUpdatedWorkloadsAsync(string AKSClusterResourceId, string _namespace, int minutesAgo)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetKubeStatefulsetsAsync(string AKSClusterResourceId, string _namespace)
    {
        var key = $"{AKSClusterResourceId}:{_namespace}";
        if (_mockStatefulSets.TryGetValue(key, out var statefulSets))
        {
            return Task.FromResult(statefulSets);
        }
        return Task.FromResult("No stateful sets configured for this resource ID and namespace.");
    }

    public Task<string> GetKubeStatefulsetSpecStatusAsync(string AKSClusterResourceId, string _namespace, string name)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetKubeStatefulSetEventsAsync(string AKSClusterResourceId, string _namespace, string name)
    {
        throw new NotImplementedException();
    }

    public Task<string> ScaleStatefulSetAsync(string AKSClusterResourceId, string _namespace, string name, int replicas)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetAPIServerStatusAsync(string AKSClusterResourceId, string timespan)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetEtcdStatusAsync(string AKSClusterResourceId, string timespan)
    {
        throw new NotImplementedException();
    }
    public Task<string> DiagnoseAKSAppAsync(string AKSClusterResourceId, string _namespace, string kind, string name)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetKubeResourceEventsAsync(string resourceId, string _namespace, string apiGroup, string kind, string name)
    {
        var eventTypes = new[] { "Normal", "Warning" };
        var random = new Random();
        var eventType = eventTypes[random.Next(eventTypes.Length)];

        string eventMessage = kind switch
        {
            "Deployment" => $"Scaled {name} replica set to 3",
            "StatefulSet" => $"Created pod: {name}-0",
            "Pod" => $"Started container: {name}",
            _ => $"Resource {kind}/{name} event generated"
        };

        return Task.FromResult($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {eventType}: {eventMessage}");
    }

    public Task<string> GetKubeResourceMetricsRangeAsync(string AKSClusterResourceId, string _namespace, string kind, string name, string metricsType, string duration, string startTime, string endTime)
    {
        var random = new Random();
        double metricValue = random.Next(10, 90);

        return Task.FromResult($"## {metricsType} Usage for {kind}/{name}\n\n" +
            $"**{kind}**: `{name}`\n" +
            $"**{metricsType} Usage**: {metricValue:F2}% of limit (at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss zz})");
    }

    public Task<string> ApplyKubernetesYamlAsync(string resourceId, string yamlContent)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(yamlContent))
            {
                return Task.FromResult("Error: YAML content is empty or null");
            }

            // Parse the provided YAML into an object to validate it
            var deserializer = new DeserializerBuilder().Build();
            var tempObj = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

            if (tempObj == null || !tempObj.ContainsKey("kind") || !tempObj.ContainsKey("apiVersion"))
            {
                return Task.FromResult("Error: Invalid YAML document: Missing 'kind' or 'apiVersion'");
            }

            var kind = tempObj["kind"].ToString();
            var apiVersion = tempObj["apiVersion"].ToString();
            string namespaceName = "default";
            string resourceName = "";

            // Extract namespace and name from metadata if present
            if (tempObj.TryGetValue("metadata", out var metadataObj) &&
                metadataObj is Dictionary<string, object> metadata)
            {
                if (metadata.TryGetValue("namespace", out var namespaceObj))
                {
                    namespaceName = namespaceObj.ToString();
                }

                if (metadata.TryGetValue("name", out var nameObj))
                {
                    resourceName = nameObj.ToString();
                }
            }

            if (string.IsNullOrEmpty(resourceName))
            {
                return Task.FromResult("Error: Resource is missing name in metadata");
            }

            // In the mock implementation, we don't actually apply anything,
            // just return a success message
            return Task.FromResult($"Mock: Successfully applied {kind}/{resourceName} in namespace '{namespaceName}'");
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error applying YAML: {ex.Message}");
        }
    }
}
