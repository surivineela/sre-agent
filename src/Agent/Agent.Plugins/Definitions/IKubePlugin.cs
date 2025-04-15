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
        Task<string> GetKubePodsAsync(string AKSClusterResourceId, string _namespace, string name);
        Task<string> GetKubeDeploymentSpecStatusAsync(string AKSClusterResourceId, string _namespace, string name);
        Task<string> GetKubeDeploymentEventsAsync(string AKSClusterResourceId, string _namespace, string name);
        Task<string> RolloutRestartDeploymentAsync(string AKSClusterResourceId, string _namespace, string name);
        Task<string> ScaleDeploymentAsync(string AKSClusterResourceId, string _namespace, string name, int replicas);
        Task<string> GetKubePodEventsAsync(string AKSClusterResourceId, string _namespace, string pod);
        Task<string> GetKubePodLogsAsync(string AKSClusterResourceId, string _namespace, string pod, int lines = 100);
        Task<string> ExecCommandInPodAsync(string AKSClusterResourceId, string _namespace, string pod, string? container, string command);
        Task<string> ListCRDsAsync(string AKSClusterResourceId);
        Task<string> ListCustomResourcesAsync(string AKSClusterResourceId, string _namespace, string apiGroup, string kind);
        Task<string> GetCustomResourceYamlAsync(string AKSClusterResourceId, string _namespace, string apiGroup, string kind, string name);
        Task<string> GetPodYamlAsync(string AKSClusterResourceId, string _namespace, string pod);
        Task<string> GetPodCpuMetricsForDeploymentAsync(string AKSClusterResourceId, string _namespace, string name, string timeRange = "5m");
        Task<string> GetPodMemoryMetricsForDeploymentAsync(string AKSClusterResourceId, string _namespace, string name);
        Task<string> GetSuccessRateMetricsAsync(string AKSClusterResourceId, string _namespace, string name, string timeRange = "5m");
        Task<string> GetRecentlyUpdatedWorkloadsAsync(string AKSClusterResourceId, string _namespace, int minutesAgo);
        Task<string> GetKubeStatefulsetsAsync(string AKSClusterResourceId, string _namespace);
        Task<string> GetKubeStatefulsetSpecStatusAsync(string AKSClusterResourceId, string _namespace, string name);
        Task<string> GetKubeStatefulSetEventsAsync(string AKSClusterResourceId, string _namespace, string name);
        Task<string> ScaleStatefulSetAsync(string AKSClusterResourceId, string _namespace, string name, int replicas);
    }
}
