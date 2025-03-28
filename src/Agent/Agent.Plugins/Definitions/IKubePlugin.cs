using Agent.Core.Models;

namespace Agent.Plugins
{
    public interface IKubePlugin
    {
        Task<string> GetKubeNamespacesAsync(string resourceId);
        Task<string> GetKubeDeploymentsAsync(string resourceId, string _namespace);
        Task<string> GetKubePodsAsync(string resourceId, string _namespace, string deployment);
        Task<string> GetKubeDeploymentSpecStatusAsync(string resourceId, string _namespace, string deployment);
        Task<string> GetKubeDeploymentEventsAsync(string resourceId, string _namespace, string deployment);
        Task<string> RolloutRestartDeploymentAsync(string resourceId, string _namespace, string deployment);
        Task<string> GetKubePodEventsAsync(string resourceId, string _namespace, string pod);
        Task<string> GetKubePodLogsAsync(string resourceId, string _namespace, string pod, int lines = 100);
        Task<string> ExecCommandInPodAsync(string resourceId, string _namespace, string pod, string? container, string command);
        Task<string> ListKubePodResourceUsageByNamespaceAsync(string resourceId, string _namespace);
        Task<string> ListCRDsAsync(string resourceId);
        Task<string> ListCustomResourcesAsync(string resourceId, string _namespace, string apiGroup, string kind);
        Task<string> GetCustomResourceYamlAsync(string resourceId, string _namespace, string apiGroup, string kind, string name);
        Task<string> GetPodYamlAsync(string resourceId, string _namespace, string pod);
    }
}