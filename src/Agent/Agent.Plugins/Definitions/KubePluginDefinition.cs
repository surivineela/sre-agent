// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    public class KubePluginDefinition
    {
        private readonly IKubePlugin _kubePlugin;

        public KubePluginDefinition(IKubePlugin kubePlugin)
        {
            _kubePlugin = kubePlugin;
        }

        [KernelFunction("get_kube_namespaces")]
        [Description(
@"Get all namespaces in the Kubernetes cluster.
Used whenever user want to list namespaces or not specified namespace when asking for resources. eg: list all namespaces in my kubernetes cluster")]
        public async Task<string> GetKubeNamespacesAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string resourceId)
        {
            return await _kubePlugin.GetKubeNamespacesAsync(resourceId);
        }

        [KernelFunction("get_kube_deployments")]
        [Description(
@"Get all deployments in the specified namespace. Deployments is the most typical workloads in Kubernetes.
Used whenever user wants to list deployments in a specific namespace. eg: list all deployments in the 'default' namespace.
It can also be invoked multiple times to list deployments in different namespaces. eg: list all deployments in the 'default' and 'kube-system' namespaces.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> GetKubeDeploymentsAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string resourceId,
              [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace)
        {
            return await _kubePlugin.GetKubeDeploymentsAsync(resourceId, _namespace);
        }

        [KernelFunction("get_kube_pods")]
        [Description(
@"Get all pods in the specified deployment and namespace.
Used whenever user wants to list pods in a specific deployment. eg: list all pods in the 'nginx-deployment' in the 'default' namespace.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> GetKubePodsAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string resourceId,
             [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
             [Description($"Name of the Kubernetes deployment, e.g. 'nginx', 'backend'")] string deployment)
        {
            return await _kubePlugin.GetKubePodsAsync(resourceId, _namespace, deployment);
        }

        [KernelFunction("get_kube_deployment_spec_status")]
        [Description(
@"Get the specification and status of a deployment in the specified namespace.
Used whenever user wants to check the detailed configuration and current status of a specific deployment.
eg: show me the spec and status of the 'nginx-deployment' in the 'default' namespace.
eg: show me the YAML of the 'nginx-deployment' in the 'default' namespace.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> GetKubeDeploymentSpecStatusAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string resourceId,
             [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
             [Description($"Name of the Kubernetes deployment, e.g. 'nginx', 'backend'")] string deployment)
        {
            return await _kubePlugin.GetKubeDeploymentSpecStatusAsync(resourceId, _namespace, deployment);
        }

        [KernelFunction("get_kube_deployment_events")]
        [Description(
@"Get the events of a deployment in the specified namespace.
Used whenever user wants to check the events or history of a specific deployment.
eg: show me the events of the 'nginx-deployment' in the 'default' namespace.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> GetKubeDeploymentEventsAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string resourceId,
              [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
             [Description($"Name of the Kubernetes deployment, e.g. 'nginx', 'backend'")] string deployment)
        {
            return await _kubePlugin.GetKubeDeploymentEventsAsync(resourceId, _namespace, deployment);
        }

        [KernelFunction("rollout_restart_deployment")]
        [Description(
@"Restart a deployment in the specified namespace.
Used whenever user wants to restart or rollout restart a deployment, it can also be used by restart pod if the pod belongs to the deployment.
eg: restart the 'nginx-deployment' in the 'default' namespace.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> RolloutRestartDeploymentAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string resourceId,
              [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
             [Description($"Name of the Kubernetes deployment, e.g. 'nginx', 'backend'")] string deployment)
        {
            return await _kubePlugin.RolloutRestartDeploymentAsync(resourceId, _namespace, deployment);
        }

        [KernelFunction("get_kube_pod_events")]
        [Description(
@"Get the events of a pod in the specified namespace.
Used whenever user wants to check the events or history of a specific pod.
eg: show me the events of the pod 'nginx-pod-xyz' in the 'default' namespace.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> GetKubePodEventsAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string resourceId,
              [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
             [Description($"Name of the Kubernetes pod, e.g. 'backend-947df49ff-l2zb4'")] string pod)
        {
            return await _kubePlugin.GetKubePodEventsAsync(resourceId, _namespace, pod);
        }

        [KernelFunction("get_kube_pod_logs")]
        [Description(
@"Get the logs of a pod in the specified namespace.
Used whenever user wants to check the logs of a specific pod.
eg: show me the last 100 lines of logs from pod 'nginx-pod-xyz' in the 'default' namespace.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> GetKubePodLogsAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string resourceId,
              [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
             [Description($"Name of the Kubernetes pod, e.g. 'backend-947df49ff-l2zb4'")] string pod,
             [Description($"Line of the logs to be print out from pods containers, it's optional, if not specified, default value is 100 lines.")] int lines = 100)
        {
            return await _kubePlugin.GetKubePodLogsAsync(resourceId, _namespace, pod, lines);
        }

        [KernelFunction("exec_command_in_pod")]
        [Description(
@"Execute a command in a pod in the specified namespace.
Used whenever user wants to run a command inside a specific pod.
eg: run 'ls -l' in pod 'nginx-pod-xyz' in the 'default' namespace.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> ExecCommandInPodAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string resourceId,
                   [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
             [Description($"Name of the Kubernetes pod, e.g. 'backend-947df49ff-l2zb4'")] string pod,
             [Description($"Container Name inside the Kubernetes pod, e.g. 'main'. The value can be optional or empty")] string? container,
             [Description($"Command to be executed inside a pod, e.g. 'ls -l', 'top'")] string command)
        {
            return await _kubePlugin.ExecCommandInPodAsync(resourceId, _namespace, pod, container, command);
        }

        [KernelFunction("list_kube_pod_resource_usage")]
        [Description(
@"List resource usage of all pods in the specified namespace.
Used whenever user wants to check CPU and memory usage of pods.
eg: show me the resource usage of all pods in the 'default' namespace.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> ListKubePodResourceUsageByNamespaceAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string resourceId,
                [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace)
        {
            return await _kubePlugin.ListKubePodResourceUsageByNamespaceAsync(resourceId, _namespace);
        }

        [KernelFunction("list_crds")]
        [Description(
@"List all Custom Resource Definitions (CRDs) in the cluster.
Used whenever user wants to check what custom resources are available in the cluster.
eg: show me all CRDs in the cluster")]
        public async Task<string> ListCRDsAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string resourceId
        )
        {
            return await _kubePlugin.ListCRDsAsync(resourceId);
        }

        [KernelFunction("list_custom_resources")]
        [Description(
@"List custom resource objects in a namespace with specific API group and kind.
Used whenever user wants to list custom resource objects like Istio VirtualServices, ArgoCD Applications, etc.
eg: list all VirtualServices in the 'istio-system' namespace.")]
        public async Task<string> ListCustomResourcesAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string resourceId,
    [Description($"Kubernetes namespace, e.g. 'default', 'istio-system'")] string _namespace,
    [Description($"API Group of the custom resource, e.g. 'networking.istio.io'")] string apiGroup,
    [Description($"Kind of the custom resource, e.g. 'VirtualService'")] string kind)
        {
            return await _kubePlugin.ListCustomResourcesAsync(resourceId, _namespace, apiGroup, kind);
        }

        [KernelFunction("get_custom_resource_yaml")]
        [Description(
@"Get the YAML representation of a custom resource object.
Used to view detailed configuration of custom resources like Istio VirtualServices or ArgoCD Applications.
eg: show me the YAML of VirtualService 'my-service' in the 'istio-system' namespace.")]
        public async Task<string> GetCustomResourceYamlAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string resourceId,
    [Description($"Kubernetes namespace, e.g. 'default', 'istio-system'")] string _namespace,
    [Description($"API Group of the custom resource, e.g. 'networking.istio.io'")] string apiGroup,
    [Description($"Kind of the custom resource, e.g. 'VirtualService'")] string kind,
    [Description($"Name of the custom resource")] string name)
        {
            return await _kubePlugin.GetCustomResourceYamlAsync(resourceId, _namespace, apiGroup, kind, name);
        }

        [KernelFunction("get_pod_yaml")]
        [Description(
@"Get the YAML representation of a pod including metadata, spec and status.
Used whenever user wants to see detailed configuration of a pod.
eg: show me the YAML of pod 'nginx-pod-xyz' in the 'default' namespace.")]
        public async Task<string> GetPodYamlAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string resourceId,
    [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
    [Description($"Name of the Kubernetes pod, e.g. 'backend-947df49ff-l2zb4'")] string pod)
        {
            return await _kubePlugin.GetPodYamlAsync(resourceId, _namespace, pod);
        }
    }
}
