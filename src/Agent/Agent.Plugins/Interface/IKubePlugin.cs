// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Core.Services;

namespace Agent.Plugins.Interface
{
    public interface IKubePlugin
    {
        Task<string> GetAKSClusterResourceIdAsync(string Subscription, string ResourceGroupName, string AKSClusterName);
        Task<string> GetKubeNamespacesAsync(string AKSClusterResourceId);
        Task<string> GetKubeDeploymentsAsync(string AKSClusterResourceId, string _namespace);
        Task<string> ListKubeResourcesAsync(string AKSClusterResourceId, string? _namespace, string kind);
        Task<string> GetKubePodsAsync(string AKSClusterResourceId, string _namespace, string kind, string name);
        Task<string> GetKubePodLogsAsync(string AKSClusterResourceId, string _namespace, string pod, string containerName = "", int lines = 100);
        Task<string> GetKubeResourceSpecStatusAsync(string AKSClusterResourceId, string? _namespace, string apiGroup, string kind, string name);
        Task<string> GetKubeResourceEventsAsync(string AKSClusterResourceId, string? _namespace, string apiGroup, string kind, string name);
        Task<string> GetKubeResourceMetricsRangeAsync(string AKSClusterResourceId, string? _namespace, string kind, string name, string metricsType, string startTime, string endTime);
        Task<string> GetCpuMetricsForWorkloadAsync(string AKSClusterResourceId, string _namespace, string workloadType, string workloadName, string timeRange = "5m");
        Task<string> GetMemoryMetricsForWorkloadAsync(string AKSClusterResourceId, string _namespace, string workloadType, string workloadName, string timeRange = "5m");
        /// <summary>
        /// Restarts a deployment by deleting its pods (NOT using rollout restart which creates new revision).
        /// Deletes pods based on deployment selector labels, allowing deployment to recreate them with same revision.
        /// </summary>
        Task<string> RolloutRestartDeploymentAsync(string AKSClusterResourceId, string _namespace, string name);
        Task<string> ScaleStatefulSetAsync(string AKSClusterResourceId, string _namespace, string name, int replicas);
        Task<string> GetRecentlyUpdatedWorkloadsAsync(string AKSClusterResourceId, string _namespace, int minutesAgo);
        Task<string> ListCRDsAsync(string AKSClusterResourceId);
        Task<string> ListCustomResourcesAsync(string AKSClusterResourceId, string _namespace, string apiGroup, string kind);
        Task<string> GetAPIServerStatusAsync(string AKSClusterResourceId, string timeRange);
        Task<string> GetEtcdStatusAsync(string AKSClusterResourceId, string timeRange);
        Task<string> DiagnoseAKSAppAsync(string AKSClusterResourceId, string _namespace, string kind, string name);
        Task<string> PatchKubernetesYamlAsync(string resourceId, string yamlContent);
        Task<string> ListWorkloadRevisions(string AKSClusterResourceId, string _namespace, string kind, string name);
        Task<CliToolExecutionResult> RunKubectlReadCommandAsync(string AKSClusterResourceId, string command);
        Task<CliToolExecutionResult> RunKubectlWriteCommandAsync(string AKSClusterResourceId, string command, string stdin = "");
        Task<CliToolExecutionResult> RunKubectlCommandHelpAsync(string AKSClusterResourceId, string command);
        Task<string> DiscoverMetricsAsync(string AKSClusterResourceId, string? namePattern, string? metricType);
        Task<string> GetMetricLabelsAsync(string AKSClusterResourceId, string metricName, string? labelName);
        Task<string> ExecutePromQLAsync(string AKSClusterResourceId, string query, string duration, string step, string? labelFilters, string? aggregateFunction, string? aggregateBy, int? limit, double? minValue);
        Task<string> ProfileAppCpuInAKSContainerAsync(string aksResourceId, string _namespace, string podName, string? targetContainerName, int durationSeconds = 30);
        Task<string> AnalyzeAppMemoryInAKSContainerAsync(string aksResourceId, string _namespace, string podName, string? targetContainerName);
        Task<CliExecutionResult> ExecuteKubectlCommandSafely(string resourceId, string command, string stdin = "", TimeSpan? timeoutMin = null);
    }
}
