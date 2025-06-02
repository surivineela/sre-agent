using Agent.Plugins.Definitions;
using Agent.Logging;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation.DiagnosticsPlugin.ComputeResourceDiagnosticStrategies;

internal sealed class KubernetesDiagnosticStrategy : ComputeResourceDiagnosticStrategyBase
{
    private readonly IKubePlugin _kubePlugin;

    internal record KubernetesAnalysisContext(
        string? Namespace,
        string? PodName,
        string? ContainerName
    );

    public KubernetesDiagnosticStrategy(ILogger<DiagnosticsPlugin> logger, IKubePlugin kubePlugin)
        : base(logger)
    {
        _kubePlugin = kubePlugin;

        _analysisHandlers = new Dictionary<AnalysisType, Func<string, ComputeResourceInfo, AnalysisType, string, Task<string>>>
        {
            { AnalysisType.Memory, AnalyzeMemoryAsync },
            { AnalysisType.Cpu, AnalyzeCPUAsync },
        };
    }

    protected static KubernetesAnalysisContext ExtractKubernetesContext(string? additionalProperties)
    {
        if (string.IsNullOrWhiteSpace(additionalProperties))
        {
            return new KubernetesAnalysisContext(null, null, null);
        }

        string? namespaceValue = null;
        string? podName = null;
        string? containerName = null;

        // Split by semicolon and process each key-value pair
        var pairs = additionalProperties.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in pairs)
        {
            var keyValue = pair.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (keyValue.Length == 2)
            {
                var key = keyValue[0].Trim().ToLowerInvariant();
                var value = keyValue[1].Trim();

                switch (key)
                {
                    case "namespace":
                        namespaceValue = value;
                        break;
                    case "pod":
                        podName = value;
                        break;
                    case "container":
                        containerName = value;
                        break;
                }
            }
        }

        return new KubernetesAnalysisContext(namespaceValue, podName, containerName);
    }

    public override bool CanHandle(ComputeResourceInfo resourceInfo)
        => resourceInfo.ResourceType == ComputeResourceType.KubernetesService;

    internal async Task<string> AnalyzeMemoryAsync(string resourceId, ComputeResourceInfo computeResourceInfo, AnalysisType analysisType, string additionalProperties)
    {
        if (computeResourceInfo.OsType != OSType.Linux)
        {
            string errorMessage = "Memory analysis is only supported for Linux containers in Kubernetes.";
            _logger.LogInternalError(errorMessage);
            throw new ArgumentException("Memory analysis is only supported for Linux containers in Kubernetes.");
        }
        return await GetMemoryAnalysis(resourceId, additionalProperties);
    }

    internal async Task<string> AnalyzeCPUAsync(string resourceId, ComputeResourceInfo computeResourceInfo, AnalysisType type, string additionalProperties)
    {
        if (computeResourceInfo.OsType != OSType.Linux)
        {
            string errorMessage = "CPU analysis is only supported for Linux containers in Kubernetes.";
            _logger.LogInternalError(errorMessage);
            throw new ArgumentException("CPU analysis is only supported for Linux containers in Kubernetes.");
        }
        return await GetCPUAnalysis(resourceId, additionalProperties);
    }

    public async Task<string> GetMemoryAnalysis(string resourceId, string? additionalProperties)
    {
        _logger.LogInternalInformation($"[GetMemoryAnalysis] Getting memory analysis for {resourceId}");

        var k8sContext = ExtractKubernetesContext(additionalProperties);

        // Validate required properties
        if (string.IsNullOrEmpty(k8sContext.Namespace))
        {
            throw new ArgumentException("Namespace is required for Kubernetes memory analysis. Please provide 'namespace' in additional properties.");
        }

        if (string.IsNullOrEmpty(k8sContext.PodName))
        {
            throw new ArgumentException("Pod name is required for Kubernetes memory analysis. Please provide 'podName' in additional properties.");
        }

        try
        {
            return await _kubePlugin.AnalyzeDotnetAppMemoryInAKSContainerAsync(
                resourceId,
                k8sContext.Namespace,
                k8sContext.PodName,
                k8sContext.ContainerName);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[GetMemoryAnalysis] Error executing memory analysis for {resourceId}: {ex.Message}");
            throw;
        }
    }

    public async Task<string> GetCPUAnalysis(string resourceId, string? additionalProperties)
    {
        _logger.LogInternalInformation($"[GetCPUAnalysis] Getting CPU analysis for {resourceId}");

        var k8sContext = ExtractKubernetesContext(additionalProperties);

        // Validate required properties
        if (string.IsNullOrEmpty(k8sContext.Namespace))
        {
            throw new ArgumentException("Namespace is required for Kubernetes CPU analysis. Please provide 'namespace' in additional properties.");
        }

        if (string.IsNullOrEmpty(k8sContext.PodName))
        {
            throw new ArgumentException("Pod name is required for Kubernetes CPU analysis. Please provide 'podName' in additional properties.");
        }

        try
        {
            int durationSeconds = 30; // Default

            return await _kubePlugin.ProfileDotnetAppCpuInAKSContainerAsync(
                resourceId,
                k8sContext.Namespace,
                k8sContext.PodName,
                k8sContext.ContainerName, // Can be null, will auto-select
                durationSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[GetCPUAnalysis] Error executing CPU analysis for {resourceId}: {ex.Message}");
            throw;
        }
    }
}
