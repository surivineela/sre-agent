using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Plugins.Mocks;
public class MockKubePlugin : IKubePlugin
{
    public Task<string> ExecCommandInPodAsync(string resourceId, string _namespace, string pod, string? container, string command)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetPodCpuMetricsForDeploymentAsync(string resourceId, string _namespace, string deployment, string timeRange = "5m")
    {
        throw new NotImplementedException();
    }

    public Task<string> GetCustomResourceYamlAsync(string resourceId, string _namespace, string apiGroup, string kind, string name)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetKubeDeploymentEventsAsync(string resourceId, string _namespace, string deployment)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetKubeDeploymentsAsync(string resourceId, string _namespace)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetKubeDeploymentSpecStatusAsync(string resourceId, string _namespace, string deployment)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetAKSClusterResourceIdAsync(string subscription, string resourceGroupName, string aksClusterName)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetKubeNamespacesAsync(string resourceId)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetKubePodEventsAsync(string resourceId, string _namespace, string pod)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetKubePodLogsAsync(string resourceId, string _namespace, string pod, int lines = 100)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetKubePodsAsync(string resourceId, string _namespace, string deployment)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetPodMemoryMetricsForDeploymentAsync(string resourceId, string _namespace, string deployment, string timeRange = "5m")
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
        throw new NotImplementedException();
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
}
