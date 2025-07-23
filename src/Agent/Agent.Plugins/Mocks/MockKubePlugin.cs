using System.Text;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Mocks;

public class MockKubePlugin : IKubePlugin
{
    public string AksClusterResourceId { get; set; } = string.Empty;

    // Add dictionaries for mock data
    private Dictionary<string, string> _mockNamespaces = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _mockDeployments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // Key: resourceId:namespace
    private Dictionary<string, string> _mockStatefulSets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // Key: resourceId:namespace
    private Dictionary<string, string> _mockSpecs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);         // Key: resourceId:namespace:apiGroup:kind:name
    private Dictionary<string, string> _mockLogs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);          // Key: resourceId:namespace:podName
    private Dictionary<string, string> _mockEvents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);        // Key: resourceId:namespace:apiGroup:kind:name
    private Dictionary<string, string> _mockPods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);          // Key: resourceId:namespace:kind:workloadName
    private Dictionary<string, (double Cpu, double Mem)> _mockPodMetrics = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase); // Key: resourceId:namespace:podName (Simplified key)
    private Dictionary<string, (double Cpu, double Mem, double Avail)> _mockMetrics = new Dictionary<string, (double, double, double)>(StringComparer.OrdinalIgnoreCase); // Key: resourceId:namespace:podName (Simplified key)
    private Dictionary<string, Action<int>> _scalingCallbacks = new Dictionary<string, Action<int>>(StringComparer.OrdinalIgnoreCase); // Key: statefulSetName
    private Dictionary<string, string> _mockRecentlyUpdated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // Key: resourceId:namespace:minutes
    private Dictionary<string, string> _mockDeploymentRevisions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // Key: resourceId:namespace:name
    private Dictionary<string, string> _mockStatefulSetRevisions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // Key: resourceId:namespace:name

    // Property to set the resource ID directly in tests if needed
    public string MockAKSResourceId { get; set; } = string.Empty;

    public MockKubePlugin()
    {
    }

    // Add configuration methods
    public void ConfigureNamespaces(string resourceId, string namespacesData)
    {
        _mockNamespaces[resourceId] = namespacesData;
        Console.WriteLine($"MockKubePlugin configured Namespaces for {resourceId}");
    }

    public void ConfigureDeployments(string resourceId, string _namespace, string deploymentsData)
    {
        var key = $"{resourceId}:{_namespace}";
        _mockDeployments[key] = deploymentsData;
        Console.WriteLine($"MockKubePlugin configured Deployments for {key}");
        // Removed the old _deploymentToPods generation logic as _mockPods is used now.
    }

    public void ConfigureStatefulSets(string resourceId, string _namespace, string statefulSetsData)
    {
        var key = $"{resourceId}:{_namespace}";
        _mockStatefulSets[key] = statefulSetsData;
        Console.WriteLine($"MockKubePlugin configured StatefulSets for {key}");
    }

    public void ConfigureSpecStatus(string resourceId, string ns, string apiGroup, string kind, string name, string yaml)
    {
        var key = $"{resourceId}:{ns}:{apiGroup}:{kind}:{name}";
        _mockSpecs[key] = yaml;
        Console.WriteLine($"MockKubePlugin configured Spec/Status for {key}");
    }

    public void ConfigurePodsForWorkload(string resourceId, string ns, string kind, string workloadName, string podList)
    {
        var key = $"{resourceId}:{ns}:{kind}:{workloadName}";
        _mockPods[key] = podList;
        Console.WriteLine($"MockKubePlugin configured Pods for {key}: {podList}");

        // Auto-generate basic pod specs/events/logs if not explicitly set
        foreach (var podName in podList.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)))
        {
            // Pod Spec Key
            var podSpecKey = $"{resourceId}:{ns}:v1:Pod:{podName}"; // Assuming v1 apiGroup for Pods
            if (!_mockSpecs.ContainsKey(podSpecKey))
            {
                string basicPodYaml = $"""
                    apiVersion: v1
                    kind: Pod
                    metadata:
                      name: {podName}
                      namespace: {ns}
                      labels: # Add basic label matching common practice
                        app: {workloadName}
                    status:
                      phase: Running
                      conditions:
                      - type: Ready
                        status: "True"
                    """;
                _mockSpecs[podSpecKey] = basicPodYaml;
                Console.WriteLine($"MockKubePlugin auto-configured basic Pod spec for {podSpecKey}");
            }
            // Pod Event Key (Note: apiGroup is often empty for core resources like Pods in field selectors)
            var podEventKey = $"{resourceId}:{ns}:::Pod:{podName}";
            if (!_mockEvents.ContainsKey(podEventKey))
            {
                _mockEvents[podEventKey] = "[2025-04-25T10:01:00Z] Normal: Started Pod"; // Default event
                Console.WriteLine($"MockKubePlugin auto-configured basic Pod event for {podEventKey}");
            }
            // Pod Log Key
            var podLogKey = $"{resourceId}:{ns}:{podName}";
            if (!_mockLogs.ContainsKey(podLogKey))
            {
                _mockLogs[podLogKey] = "Default mock log entry.";
                Console.WriteLine($"MockKubePlugin auto-configured basic Pod log for {podLogKey}");
            }
        }
    }

    public void ConfigureLogs(string resourceId, string ns, string podName, string logContent)
    {
        var key = $"{resourceId}:{ns}:{podName}";
        _mockLogs[key] = logContent;
        Console.WriteLine($"MockKubePlugin configured Logs for {key}");
    }
    public void ConfigureEvents(string resourceId, string ns, string apiGroup, string kind, string name, string eventContent)
    {
        var key = $"{resourceId}:{ns}:{apiGroup}:{kind}:{name}";
        _mockEvents[key] = eventContent;
        Console.WriteLine($"MockKubePlugin configured Events for {key}");
    }

    public void ConfigureWorkloadMetrics(string resourceId, string ns, string workloadType, string workloadName, double cpuPercent, double memPercent, double availPercent)
    {
        var key = $"{resourceId}:{ns}:{workloadType}:{workloadName}";
        _mockMetrics[key] = (cpuPercent, memPercent, availPercent);
        Console.WriteLine($"MockKubePlugin configured Metrics for {key}: CPU={cpuPercent}%, Mem={memPercent}%, Avail={availPercent}%");
    }


    public void ConfigureMetrics(string resourceId, string ns, string workloadType, string workloadName, string podName, double cpuPercent, double memPercent)
    {
        var key = $"{resourceId}:{ns}:{podName}"; // Key by pod
        _mockPodMetrics[key] = (cpuPercent, memPercent);
        Console.WriteLine($"MockKubePlugin configured Metrics for {key}: CPU={cpuPercent}%, Mem={memPercent}%");
    }

    public void ConfigureRecentlyUpdated(string resourceId, string ns, int minutes, string result)
    {
        var key = $"{resourceId}:{ns}:{minutes}";
        _mockRecentlyUpdated[key] = result;
        Console.WriteLine($"MockKubePlugin configured Recently Updated for {key}");
    }

    public void SetScalingCallback(string statefulSetName, Action<int> callback)
    {
        _scalingCallbacks[statefulSetName] = callback;
        Console.WriteLine($"MockKubePlugin configured Scaling Callback for {statefulSetName}");
    }

    public void ConfigureDeploymentRevisions(string resourceId, string _namespace, string name, string revisions)
    {
        var key = $"{resourceId}:{_namespace}:{name}";
        _mockDeploymentRevisions[key] = revisions;
        Console.WriteLine($"MockKubePlugin configured Deployment Revisions for {key}");
    }

    public void ConfigureStatefulSetRevisions(string resourceId, string _namespace, string name, string revisions)
    {
        var key = $"{resourceId}:{_namespace}:{name}";
        _mockStatefulSetRevisions[key] = revisions;
        Console.WriteLine($"MockKubePlugin configured StatefulSet Revisions for {key}");
    }

    // --- IKubePlugin Implementation ---

    public Task<string> GetAKSClusterResourceIdAsync(string subscription, string resourceGroupName, string aksClusterName)
    {
        AksClusterResourceId = $"/subscriptions/{subscription}/resourceGroups/{resourceGroupName}/providers/Microsoft.ContainerService/managedClusters/{aksClusterName}";
        return Task.FromResult($"AKSClusterResourceID is **'/subscriptions/{subscription}/resourceGroups/{resourceGroupName}/providers/Microsoft.ContainerService/managedClusters/{aksClusterName}'**");
    }

    // GetKubeNamespacesAsync: Use configured mock data.
    public Task<string> GetKubeNamespacesAsync(string resourceId) // Parameter name matches interface
    {
        if (_mockNamespaces.TryGetValue(resourceId, out var namespaces))
        {
            Console.WriteLine($"MockKubePlugin: GetKubeNamespacesAsync found for {resourceId}");
            return Task.FromResult(namespaces);
        }
        Console.WriteLine($"WARN: MockKubePlugin: GetKubeNamespacesAsync NOT FOUND for {resourceId}");
        return Task.FromResult("Mock Error: No namespaces configured for this resource ID.");
    }

    // GetKubeDeploymentsAsync: Use configured mock data.
    public Task<string> GetKubeDeploymentsAsync(string resourceId, string _namespace) // Parameter name matches interface
    {
        var key = $"{resourceId}:{_namespace}";
        if (_mockDeployments.TryGetValue(key, out var deployments))
        {
            Console.WriteLine($"MockKubePlugin: GetKubeDeploymentsAsync found for {key}");
            return Task.FromResult(deployments);
        }
        Console.WriteLine($"WARN: MockKubePlugin: GetKubeDeploymentsAsync NOT FOUND for {key}");
        return Task.FromResult("Mock Error: No deployments configured for this key.");
    }

    // ListKubeResourcesAsync: Use configured mock data.
    public Task<string> ListKubeResourcesAsync(string resourceId, string? _namespace, string kind) // Parameter name matches interface
    {
        switch (kind.ToLowerInvariant())
        {
            case "namespace":
                return GetKubeNamespacesAsync(resourceId);
            case "deployment":
                return GetKubeDeploymentsAsync(resourceId, _namespace ?? string.Empty);
            case "statefulset":
                var key = $"{resourceId}:{_namespace}";
                if (_mockStatefulSets.TryGetValue(key, out var statefulSets))
                {
                    Console.WriteLine($"MockKubePlugin: ListKubeResourcesAsync found for {key}");
                    return Task.FromResult(statefulSets);
                }
                Console.WriteLine($"WARN: MockKubePlugin: ListKubeResourcesAsync NOT FOUND for {key}");
                return Task.FromResult("Mock Error: No stateful sets configured for this key.");
            default:
                return Task.FromResult($"Mock Error: Unsupported kind '{kind}' for ListKubeResourcesAsync.");
        }

    }

    // GetKubePodsAsync: Use configured mock data (_mockPods).
    public Task<string> GetKubePodsAsync(string resourceId, string _namespace, string kind, string name) // Parameter names match interface
    {
        var key = $"{resourceId}:{_namespace}:{kind}:{name}";
        if (_mockPods.TryGetValue(key, out var pods))
        {
            Console.WriteLine($"MockKubePlugin: GetKubePodsAsync found for {key}");
            return Task.FromResult(pods);
        }
        Console.WriteLine($"WARN: MockKubePlugin: GetKubePodsAsync NOT FOUND for {key}");
        // Return empty string or specific error? Empty string might be safer for agent flow.
        return Task.FromResult($""); // Return empty instead of error
    }

    // GetKubeResourceSpecStatusAsync: Use configured mock data (_mockSpecs).
    // This combines the previous GetKubeDeploymentSpecStatusAsync, GetKubeStatefulsetSpecStatusAsync, GetKubePodSpecStatusAsync etc.
    public Task<string> GetKubeResourceSpecStatusAsync(string resourceId, string? _namespace, string apiGroup, string kind, string name)
    {
        var key = $"{resourceId}:{_namespace}:{apiGroup}:{kind}:{name}";
        // Handle potential variations (e.g., pod spec might be requested with empty apiGroup)
        if (string.IsNullOrEmpty(apiGroup) && kind.Equals("Pod", StringComparison.OrdinalIgnoreCase))
        {
            key = $"{resourceId}:{_namespace}:v1:Pod:{name}"; // Try with v1 explicitly for Pods
        }
        else if (string.IsNullOrEmpty(apiGroup) && kind.Equals("Service", StringComparison.OrdinalIgnoreCase))
        {
            key = $"{resourceId}:{_namespace}:v1:Service:{name}"; // Try with v1 explicitly for Service
        }
        else if (string.IsNullOrEmpty(apiGroup) && (kind.Equals("Deployment", StringComparison.OrdinalIgnoreCase) || kind.Equals("StatefulSet", StringComparison.OrdinalIgnoreCase)))
        {
            key = $"{resourceId}:{_namespace}:apps/v1:{kind}:{name}"; // Try with apps/v1 for Deploy/STS
        }


        if (_mockSpecs.TryGetValue(key, out var spec))
        {
            Console.WriteLine($"MockKubePlugin: GetKubeResourceSpecStatusAsync found for {key}");
            return Task.FromResult(spec);
        }
        Console.WriteLine($"WARN: MockKubePlugin: GetKubeResourceSpecStatusAsync NOT FOUND for {key}");
        return Task.FromResult($"Mock Error: Spec for {kind}/{name} not configured.");
    }


    // GetKubeResourceEventsAsync: Use configured mock data (_mockEvents).
    public Task<string> GetKubeResourceEventsAsync(string resourceId, string? _namespace, string apiGroup, string kind, string name)
    {
        var key = $"{resourceId}:{_namespace}:{apiGroup}:{kind}:{name}";
        if (_mockEvents.TryGetValue(key, out var events))
        {
            Console.WriteLine($"MockKubePlugin: GetKubeResourceEventsAsync found for {key}");
            return Task.FromResult(events);
        }
        Console.WriteLine($"WARN: MockKubePlugin: GetKubeResourceEventsAsync NOT FOUND for {key}");
        return Task.FromResult($"Mock: No events found for {kind}/{name}"); // Return no events instead of error
    }

    // GetKubePodLogsAsync: Use configured mock data (_mockLogs).
    public Task<string> GetKubePodLogsAsync(string resourceId, string _namespace, string pod, string containerName = "", int lines = 100) // Parameter names match interface
    {
        var key = $"{resourceId}:{_namespace}:{pod}";
        if (_mockLogs.TryGetValue(key, out var logs))
        {
            Console.WriteLine($"MockKubePlugin: GetKubePodLogsAsync found for {key}");
            // Basic simulation of tailing lines
            var logLines = logs.Split('\n');
            var subset = logLines.TakeLast(lines);
            return Task.FromResult(string.Join("\n", subset));
        }
        Console.WriteLine($"WARN: MockKubePlugin: GetKubePodLogsAsync NOT FOUND for {key}");
        return Task.FromResult($"Mock Error: Logs for pod {pod} not configured.");
    }

    // ScaleStatefulSetAsync: Use configured callback and mock data.
    public Task<string> ScaleStatefulSetAsync(string resourceId, string _namespace, string name, int replicas) // Parameter names match interface
    {
        Console.WriteLine($"MockKubePlugin: ScaleStatefulSetAsync called for {name} to {replicas} replicas.");
        if (_scalingCallbacks.TryGetValue(name, out var callback))
        {
            Console.WriteLine($"MockKubePlugin: Executing scaling callback for {name}.");
            callback?.Invoke(replicas); // Update internal mock state via callback
        }
        else
        {
            Console.WriteLine($"WARN: MockKubePlugin: No scaling callback configured for {name}. State will not be updated automatically.");
            // Optionally, add crude state update here if callback isn't mandatory for all tests
        }
        return Task.FromResult($"Mock: StatefulSet {name} scaling to {replicas} initiated.");
    }

    // GetRecentlyUpdatedWorkloadsAsync: Use configured mock data.
    public Task<string> GetRecentlyUpdatedWorkloadsAsync(string resourceId, string _namespace, int minutesAgo) // Parameter names match interface
    {
        var key = $"{resourceId}:{_namespace}:{minutesAgo}";
        if (_mockRecentlyUpdated.TryGetValue(key, out var result))
        {
            Console.WriteLine($"MockKubePlugin: GetRecentlyUpdatedWorkloadsAsync found for {key}");
            return Task.FromResult(result);
        }
        Console.WriteLine($"WARN: MockKubePlugin: GetRecentlyUpdatedWorkloadsAsync NOT FOUND for {key}");
        return Task.FromResult($"Mock: No recently updated workloads configured for namespace {_namespace} within {minutesAgo} minutes.");
    }

    public Task<string> ListWorkloadRevisions(string resourceId, string _namespace, string kind, string name)
    {
        Console.WriteLine($"MockKubePlugin: ListWorkloadRevisions called for {kind}/{name} in namespace {_namespace}");
        var key = $"{resourceId}:{_namespace}:{name}";

        switch (kind.ToLowerInvariant())
        {
            case "deployment":
                if (_mockDeploymentRevisions.TryGetValue(key, out var deployRevisions))
                {
                    Console.WriteLine($"MockKubePlugin: Found deployment revisions for {key}");
                    return Task.FromResult(deployRevisions);
                }
                Console.WriteLine($"WARN: MockKubePlugin: No deployment revisions found for {key}");
                return Task.FromResult($"Mock: No revisions found for Deployment '{name}' in namespace '{_namespace}'");

            case "statefulset":
                if (_mockStatefulSetRevisions.TryGetValue(key, out var stsRevisions))
                {
                    Console.WriteLine($"MockKubePlugin: Found statefulset revisions for {key}");
                    return Task.FromResult(stsRevisions);
                }
                Console.WriteLine($"WARN: MockKubePlugin: No statefulset revisions found for {key}");
                return Task.FromResult($"Mock: No revisions found for StatefulSet '{name}' in namespace '{_namespace}'");

            default:
                Console.WriteLine($"WARN: MockKubePlugin: Unsupported kind {kind} for ListWorkloadRevisions");
                return Task.FromResult($"Mock Error: Workload kind '{kind}' is not supported for revision listing. Only Deployment and StatefulSet are supported.");
        }
    }

    // --- Metrics Methods (Implement using _mockPodMetrics) ---
    // NOTE: These should ideally be added to the IKubePlugin interface as well.

    public Task<string> GetCpuMetricsForWorkloadAsync(string resourceId, string _namespace, string workloadType, string workloadName, string timeRange = "5m")
    {
        Console.WriteLine($"MockKubePlugin: GetCpuMetricsForWorkloadAsync called for {resourceId}:{_namespace}:{workloadType}:{workloadName}");
        // Determine the expected pods for this workload
        var podListKey = $"{resourceId}:{_namespace}:{workloadType}:{workloadName}";
        List<string> expectedPods = new List<string>();
        if (_mockPods.TryGetValue(podListKey, out var podListStr) && !string.IsNullOrWhiteSpace(podListStr))
        {
            expectedPods = podListStr.Split(',').Select(p => p.Trim()).ToList();
        }
        else
        {
            Console.WriteLine($"WARN: MockKubePlugin: Pod list not found for workload key {podListKey} during CPU metric lookup. Results may be incomplete/incorrect.");
            // Decide behavior: return empty, error, or all metrics in namespace? Returning filtered matches is safer.
        }


        var sb = new System.Text.StringBuilder();
        bool foundMetrics = false;

        // Iterate through all configured metrics and filter by expected pods
        foreach (var kvp in _mockPodMetrics)
        {
            // Key format: resourceId:namespace:podName
            var keyParts = kvp.Key.Split(':');
            if (keyParts.Length == 3 && keyParts[0].Equals(resourceId, StringComparison.OrdinalIgnoreCase) && keyParts[1].Equals(_namespace, StringComparison.OrdinalIgnoreCase))
            {
                var podName = keyParts[2];
                // Only include if pod is expected for this workload OR if expectedPods list is empty (less strict fallback)
                if (expectedPods.Contains(podName) || !expectedPods.Any())
                {
                    if (!foundMetrics)
                    {
                        // Add header only once metrics are found
                        sb.AppendLine($"## CPU Usage for {workloadType}/{workloadName}");
                        foundMetrics = true;
                    }
                    sb.AppendLine($"**Pod**: `{podName}`");
                    sb.AppendLine($"**CPU Usage**: {kvp.Value.Cpu:F2}% of limit (at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss zz})"); // Use current time for mock
                    sb.AppendLine();
                }
            }
        }

        if (!foundMetrics)
        {
            Console.WriteLine($"WARN: MockKubePlugin: No relevant CPU Metrics found for workload {workloadType}/{workloadName} in {_namespace}");
            return Task.FromResult($"Mock Error: No relevant CPU Metrics found for workload {workloadName}."); // Or return an empty success message?
        }

        return Task.FromResult(sb.ToString().Trim());
    }

    public Task<string> GetMemoryMetricsForWorkloadAsync(string resourceId, string _namespace, string workloadType, string workloadName, string timeRange = "5m")
    {
        Console.WriteLine($"MockKubePlugin: GetMemoryMetricsForWorkloadAsync called for {resourceId}:{_namespace}:{workloadType}:{workloadName}");
        // Determine the expected pods for this workload
        var podListKey = $"{resourceId}:{_namespace}:{workloadType}:{workloadName}";
        List<string> expectedPods = new List<string>();
        if (_mockPods.TryGetValue(podListKey, out var podListStr) && !string.IsNullOrWhiteSpace(podListStr))
        {
            expectedPods = podListStr.Split(',').Select(p => p.Trim()).ToList();
        }
        else
        {
            Console.WriteLine($"WARN: MockKubePlugin: Pod list not found for workload key {podListKey} during Memory metric lookup. Results may be incomplete/incorrect.");
        }

        var sb = new System.Text.StringBuilder();
        bool foundMetrics = false;

        // Iterate through all configured metrics and filter by expected pods
        foreach (var kvp in _mockPodMetrics)
        {
            var keyParts = kvp.Key.Split(':');
            if (keyParts.Length == 3 && keyParts[0].Equals(resourceId, StringComparison.OrdinalIgnoreCase) && keyParts[1].Equals(_namespace, StringComparison.OrdinalIgnoreCase))
            {
                var podName = keyParts[2];
                // Only include if pod is expected for this workload OR if expectedPods list is empty
                if (expectedPods.Contains(podName) || !expectedPods.Any())
                {
                    if (!foundMetrics)
                    {
                        sb.AppendLine($"## Memory Usage for {workloadType}/{workloadName}");
                        sb.AppendLine();
                        foundMetrics = true;
                    }
                    sb.AppendLine($"**Pod**: `{podName}`");
                    sb.AppendLine($"**Memory Usage**: {kvp.Value.Mem:F2}% of limit (at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss zz})");
                    sb.AppendLine();
                }
            }
        }


        if (!foundMetrics)
        {
            Console.WriteLine($"WARN: MockKubePlugin: No relevant Memory Metrics found for workload {workloadType}/{workloadName} in {_namespace}");
            return Task.FromResult($"Mock Error: No relevant Memory Metrics found for workload {workloadName}.");
        }

        return Task.FromResult(sb.ToString().Trim());
    }


    // --- Other IKubePlugin Methods (Implement as needed or leave as NotImplemented) ---

    public Task<string> ListCRDsAsync(string resourceId)
    {
        Console.WriteLine($"WARN: MockKubePlugin: ListCRDsAsync NOT IMPLEMENTED");
        throw new NotImplementedException();
    }

    public Task<string> ListCustomResourcesAsync(string resourceId, string _namespace, string apiGroup, string kind)
    {
        Console.WriteLine($"WARN: MockKubePlugin: ListCustomResourcesAsync NOT IMPLEMENTED");
        throw new NotImplementedException();
    }

    public Task<string> RolloutRestartDeploymentAsync(string resourceId, string _namespace, string name) // Parameter name matches interface
    {
        Console.WriteLine($"WARN: MockKubePlugin: RolloutRestartDeploymentAsync (restart by deleting pods) NOT IMPLEMENTED");
        throw new NotImplementedException();
    }

    public Task<string> GetKubeResourceMetricsRangeAsync(string resourceId, string? _namespace, string kind, string name, string metricsType, string startTime, string endTime) // Parameter names match interface
    {
        // Simple random implementation if needed for other tests, but prefer explicit configuration
        Console.WriteLine($"WARN: MockKubePlugin: GetKubeResourceMetricsRangeAsync started ");
        string key = $"{resourceId}:{_namespace}:{kind}:{name}";
        if (!_mockMetrics.TryGetValue(key, out var metrics))
        {
            Console.WriteLine($"WARN: MockKubePlugin: No metrics found for {key}");
            return Task.FromResult($"No metrics data available for {kind}/{name}");
        }

        // Parse start and end times
        if (!DateTime.TryParse(startTime, out DateTime start))
            start = DateTime.UtcNow.AddHours(-1);
        if (!DateTime.TryParse(endTime, out DateTime end))
            end = DateTime.UtcNow;

        // Determine which metric to use from the tuple
        double baseMetricValue;
        string capitalizedMetricType = metricsType.ToUpperInvariant();
        switch (metricsType.ToLowerInvariant())
        {
            case "cpu":
                baseMetricValue = metrics.Cpu;
                break;
            case "memory":
                baseMetricValue = metrics.Mem;
                break;
            case "availability":
                baseMetricValue = metrics.Avail;
                break;
            default:
                baseMetricValue = 50.0; // Default fallback
                break;
        }

        // Generate 10 timestamps between start and end
        TimeSpan interval = (end - start) / 9; // 9 intervals for 10 points
        var sb = new StringBuilder();

        for (int i = 0; i < 10; i++)
        {
            DateTime pointTime = start.Add(interval * i);
            double metricValue = baseMetricValue;
            sb.AppendLine($"{pointTime:yyyy-MM-ddTHH:mm:ss}|{metricValue:F2}|{capitalizedMetricType} Usage");
        }

        return Task.FromResult(sb.ToString());
    }

    public Task<string> GetAPIServerStatusAsync(string resourceId, string timeRange) // Parameter names match interface
    {
        Console.WriteLine($"WARN: MockKubePlugin: GetAPIServerStatusAsync NOT IMPLEMENTED");
        throw new NotImplementedException();
    }

    public Task<string> GetEtcdStatusAsync(string resourceId, string timeRange) // Parameter names match interface
    {
        Console.WriteLine($"WARN: MockKubePlugin: GetEtcdStatusAsync NOT IMPLEMENTED");
        throw new NotImplementedException();
    }

    public async Task<string> DiagnoseAKSAppAsync(string resourceId, string _namespace, string kind, string name)
    {
        Console.WriteLine($"---> MockKubePlugin: DiagnoseAKSAppAsync called for {resourceId}:{_namespace}:{kind}:{name}");
        var diagnosis = new System.Text.StringBuilder();
        string podListStr = string.Empty;
        List<string> podNames = new List<string>();

        string workloadApiGroup = (kind.Equals("Deployment", StringComparison.OrdinalIgnoreCase) || kind.Equals("StatefulSet", StringComparison.OrdinalIgnoreCase))
                                  ? "apps/v1" : ""; // Default assumption

        try
        {
            // 1. Get Spec/Status for the main resource
            diagnosis.AppendLine($"## Diagnosis for {kind}/{name} in namespace {_namespace}");
            try
            {
                string specStatus = await this.GetKubeResourceSpecStatusAsync(resourceId, _namespace, workloadApiGroup, kind, name);
                diagnosis.AppendLine("📝 Status/events:");
                // Simple summary - real agent might parse YAML, but mock can be simpler
                if (specStatus.Contains("Mock Error:"))
                {
                    diagnosis.AppendLine($"- {specStatus}");
                }
                else if (specStatus.Contains("replicas:") && (specStatus.Contains("readyReplicas:") || specStatus.Contains("currentReplicas:")))
                { // Crude check for STS/Deploy status
                    diagnosis.AppendLine($"- Resource status seems healthy based on spec presence."); // Simplified mock summary
                }
                else
                {
                    diagnosis.AppendLine($"- Spec/Status retrieved (content length: {specStatus.Length}).");
                }

                string events = await this.GetKubeResourceEventsAsync(resourceId, _namespace, workloadApiGroup, kind, name);
                if (events.Contains("Mock Error:") || events.Contains("Mock: No events"))
                {
                    diagnosis.AppendLine("- No specific warning/error events found for the resource.");
                }
                else
                {
                    diagnosis.AppendLine($"- Resource Events: {events.Split('\n').FirstOrDefault() ?? events}"); // Show first line
                }
            }
            catch (Exception ex) { diagnosis.AppendLine($"- Error getting resource spec/status/events: {ex.Message}"); }


            // 2. Get Pods for the workload
            diagnosis.AppendLine("\n👁️ Pod status/events:");
            try
            {
                podListStr = await this.GetKubePodsAsync(resourceId, _namespace, kind, name);
                if (!string.IsNullOrWhiteSpace(podListStr) && !podListStr.Contains("Mock Error:"))
                {
                    podNames = podListStr.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
                    diagnosis.AppendLine($"- Found {podNames.Count} pod(s): {podListStr}");

                    // 3. Get Diagnostics for each Pod (Status, Events, Logs) - Limit for brevity in mock
                    int podsToDetail = Math.Min(podNames.Count, 2); // Check details for max 2 pods in mock summary
                    for (int i = 0; i < podsToDetail; i++)
                    {
                        var podName = podNames[i];
                        diagnosis.AppendLine($"--- Pod: {podName} ---");
                        try
                        {
                            string podStatus = await this.GetKubeResourceSpecStatusAsync(resourceId, _namespace, "v1", "Pod", podName); // Use v1 for Pod kind
                            if (podStatus.Contains("Mock Error:"))
                            {
                                diagnosis.AppendLine($"- Pod Status: {podStatus}");
                            }
                            else if (podStatus.Contains("phase: Running") && podStatus.Contains("status: \"True\""))
                            { // Crude check
                                diagnosis.AppendLine($"- Pod Status: Running/Ready.");
                            }
                            else
                            {
                                diagnosis.AppendLine($"- Pod Status: Retrieved (content length: {podStatus.Length}).");
                            }

                            string podEvents = await this.GetKubeResourceEventsAsync(resourceId, _namespace, "", "Pod", podName); // Empty apiGroup for Pod events
                            if (podEvents.Contains("Mock Error:") || podEvents.Contains("Mock: No events"))
                            {
                                diagnosis.AppendLine("- Pod Events: No specific warning/error events found.");
                            }
                            else
                            {
                                diagnosis.AppendLine($"- Pod Events: {podEvents.Split('\n').FirstOrDefault() ?? podEvents}");
                            }

                            string podLogs = await this.GetKubePodLogsAsync(resourceId, _namespace, podName, lines: 20); // Get fewer lines for summary
                            if (podLogs.Contains("Mock Error:"))
                            {
                                diagnosis.AppendLine($"- Pod Logs: {podLogs}");
                            }
                            else
                            {
                                // Simple log summary
                                string logSummary = podLogs.Length > 100 ? podLogs.Substring(0, 100) + "..." : podLogs;
                                diagnosis.AppendLine($"- Pod Logs: Snippet: {logSummary}");
                            }
                        }
                        catch (Exception podEx) { diagnosis.AppendLine($"- Error getting details for pod {podName}: {podEx.Message}"); }
                    }
                    if (podNames.Count > podsToDetail)
                    {
                        diagnosis.AppendLine($"--- (Details for remaining {podNames.Count - podsToDetail} pods omitted in mock summary) ---");
                    }
                }
                else
                {
                    diagnosis.AppendLine("- No pods found for this workload or error retrieving pods.");
                }
            }
            catch (Exception ex) { diagnosis.AppendLine($"- Error getting pods: {ex.Message}"); }


            // 4. Get Metrics (CPU & Memory)
            diagnosis.AppendLine("\n✅ Metrics:");
            try
            {
                string cpuMetrics = await this.GetCpuMetricsForWorkloadAsync(resourceId, _namespace, kind, name);
                if (cpuMetrics.Contains("Mock Error:"))
                {
                    diagnosis.AppendLine($"- CPU: {cpuMetrics}");
                }
                else
                {
                    // Extract first few lines for summary
                    var cpuLines = cpuMetrics.Split('\n').Take(4); // Header + first pod usually
                    diagnosis.AppendLine("- CPU Usage:");
                    foreach (var line in cpuLines) { diagnosis.AppendLine($"  {line}"); }
                    if (cpuMetrics.Split('\n').Length > 4) { diagnosis.AppendLine("  (... more pods ...)"); }
                }
            }
            catch (Exception ex) { diagnosis.AppendLine($"- Error getting CPU metrics: {ex.Message}"); }

            try
            {
                string memMetrics = await this.GetMemoryMetricsForWorkloadAsync(resourceId, _namespace, kind, name);
                if (memMetrics.Contains("Mock Error:"))
                {
                    diagnosis.AppendLine($"- Memory: {memMetrics}");
                }
                else
                {
                    var memLines = memMetrics.Split('\n').Take(4);
                    diagnosis.AppendLine("- Memory Usage:");
                    foreach (var line in memLines) { diagnosis.AppendLine($"  {line}"); }
                    if (memMetrics.Split('\n').Length > 4) { diagnosis.AppendLine("  (... more pods ...)"); }
                }
            }
            catch (Exception ex) { diagnosis.AppendLine($"- Error getting Memory metrics: {ex.Message}"); }

            Console.WriteLine($"<--- MockKubePlugin: DiagnoseAKSAppAsync for {kind}/{name} returning summary.");
            return diagnosis.ToString();
        }
        catch (Exception generalEx)
        {
            Console.WriteLine($"<--- MockKubePlugin: Error during DiagnoseAKSAppAsync for {kind}/{name}: {generalEx}");
            return $"Mock Error: Unexpected failure during diagnosis of {kind}/{name}: {generalEx.Message}";
        }
    }

    public Task<string> PatchKubernetesYamlAsync(string resourceId, string yamlContent)
    {
        Console.WriteLine($"MockKubePlugin: PatchKubernetesYamlAsync called for resourceId {resourceId}");
        // Basic validation and success message like before
        try { /* ... validation logic ... */ } catch { /* ... error handling ... */ }
        // Extract kind/name for logging if possible
        return Task.FromResult($"Mock: Successfully applied YAML content.");
    }

    public Task<string> RunKubectlReadCommandAsync(string AKSClusterResourceId, string command)
    {
        throw new NotImplementedException();
    }

    public Task<string> RunKubectlWriteCommandAsync(string AKSClusterResourceId, string command, string stdin = "")
    {
        throw new NotImplementedException();
    }

    public Task<string> RunKubectlCommandHelpAsync(string AKSClusterResourceId, string command)
    {
        throw new NotImplementedException();
    }

    public Task<string> ProfileDotnetAppCpuInAKSContainerAsync(string aksResourceId, string _namespace, string podName, string? targetContainerName, int durationSeconds = 30)
    {
        throw new NotImplementedException();
    }

    public Task<string> AnalyzeDotnetAppMemoryInAKSContainerAsync(string aksResourceId, string _namespace, string podName, string? targetContainerName)
    {
        throw new NotImplementedException();
    }

    Task<string> IKubePlugin.DiscoverMetricsAsync(string AKSClusterResourceId, string? namePattern, string? metricType)
    {
        throw new NotImplementedException();
    }

    Task<string> IKubePlugin.GetMetricLabelsAsync(string AKSClusterResourceId, string metricName, string? labelName)
    {
        throw new NotImplementedException();
    }

    Task<string> IKubePlugin.ExecutePromQLAsync(string AKSClusterResourceId, string query, string duration, string step, string? labelFilters, string? aggregateFunction, string? aggregateBy, int? limit, double? minValue)
    {
        throw new NotImplementedException();
    }
    public Task<CliExecutionResult> ExecuteKubectlCommandSafely(string resourceId, string command, string stdin = "")
    {
        throw new NotImplementedException();
    }
}
