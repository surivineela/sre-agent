// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins
{
    public interface IKubePlugin
    {
        Task<string> GetAKSClusterResourceIdAsync(string Subscription, string ResourceGroupName, string AKSClusterName);
        Task<string> GetKubeNamespacesAsync(string AKSClusterResourceId);
        Task<string> GetKubeDeploymentsAsync(string AKSClusterResourceId, string _namespace);
        Task<string> ListKubeResourcesAsync(string AKSClusterResourceId, string _namespace, string kind);
        Task<string> GetKubePodsAsync(string AKSClusterResourceId, string _namespace, string kind, string name);
        Task<string> GetKubePodLogsAsync(string AKSClusterResourceId, string _namespace, string pod, string containerName = "", int lines = 100);
        Task<string> GetKubeResourceSpecStatusAsync(string AKSClusterResourceId, string _namespace, string apiGroup, string kind, string name);
        Task<string> GetKubeResourceEventsAsync(string AKSClusterResourceId, string _namespace, string apiGroup, string kind, string name);
        Task<string> GetKubeResourceMetricsRangeAsync(string AKSClusterResourceId, string _namespace, string kind, string name, string metricsType, string startTime, string endTime);
        Task<string> GetCpuMetricsForWorkloadAsync(string AKSClusterResourceId, string _namespace, string workloadType, string workloadName, string timeRange = "5m");
        Task<string> GetMemoryMetricsForWorkloadAsync(string AKSClusterResourceId, string _namespace, string workloadType, string workloadName, string timeRange = "5m");
        Task<string> RolloutRestartDeploymentAsync(string AKSClusterResourceId, string _namespace, string name);
        Task<string> ScaleDeploymentAsync(string AKSClusterResourceId, string _namespace, string name, int replicas);
        Task<string> ScaleStatefulSetAsync(string AKSClusterResourceId, string _namespace, string name, int replicas);
        Task<string> GetRecentlyUpdatedWorkloadsAsync(string AKSClusterResourceId, string _namespace, int minutesAgo);
        Task<string> ListCRDsAsync(string AKSClusterResourceId);
        Task<string> ListCustomResourcesAsync(string AKSClusterResourceId, string _namespace, string apiGroup, string kind);
        Task<string> GetAPIServerStatusAsync(string AKSClusterResourceId, string timeRange);
        Task<string> GetEtcdStatusAsync(string AKSClusterResourceId, string timeRange);
        Task<string> DiagnoseAKSAppAsync(string AKSClusterResourceId, string _namespace, string kind, string name);
        Task<string> PatchKubernetesYamlAsync(string resourceId, string yamlContent);
        Task<IDictionary<string, string>> GetNsgRulesForWorkloadAsync(string aksResourceId, string _namespace, string kind, string workloadName);
        Task<string> ListWorkloadRevisions(string AKSClusterResourceId, string _namespace, string kind, string name);
        Task<string> RunKubectlGetCommandAsync(string AKSClusterResourceId, string command);
    }
}
