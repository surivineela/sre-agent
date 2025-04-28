// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Attributes;
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

        [KernelFunction("GetAKSClusterResourceIdSetThreadContextProperties")]
        [Description(
        @"Get AKS cluster resource ID from subscription, resource group name and AKS cluster name.
        Used whenever user want to access AKS cluster but didn't specify the resource ID.
        ")]
        public async Task<string> GetAKSClusterResourceIdAsync(
            [Description("The subscription ID of Azure Kubernetes Service")] string Subscription,
            [Description("The name of resource group.")] string ResourceGroupName,
            [Description("The name of the Azure Kubernetes Service cluster.")] string AKSClusterName)
        {
            var AksResourceId = await _kubePlugin.GetAKSClusterResourceIdAsync(Subscription, ResourceGroupName, AKSClusterName);
            return AksResourceId;
        }

        [KernelFunction("get_kube_namespaces")]
        [Description(
@"Get all namespaces in the Kubernetes cluster.
Used whenever user want to list namespaces or not specified namespace when asking for resources. eg: list all namespaces in my kubernetes cluster")]
        public async Task<string> GetKubeNamespacesAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId)
        {
            return await _kubePlugin.GetKubeNamespacesAsync(AKSClusterResourceId);
        }

        [KernelFunction("get_kube_deployments")]
        [Description(
@"Get all deployments in the specified namespace. Deployments is the most typical workloads in Kubernetes.
Used whenever user wants to list deployments in a specific namespace. eg: list all deployments in the 'default' namespace.
It can also be invoked multiple times to list deployments in different namespaces. eg: list all deployments in the 'default' and 'kube-system' namespaces.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> GetKubeDeploymentsAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId,
              [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace)
        {
            return await _kubePlugin.GetKubeDeploymentsAsync(AKSClusterResourceId, _namespace);
        }

        [KernelFunction("get_kube_pods")]
        [Description(
@"Get all pods belong to the specific resource and namespace.
Used whenever user wants to list pods in a specific deployment or statefulset. eg: list all pods in the 'nginx-deployment' in the 'default' namespace.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> GetKubePodsAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId,
             [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
             [Description($"Kubernetes resource kind, e.g. 'deployment', 'statefulset'")] string kind,
             [Description($"Name of the Kubernetes resource, e.g. 'nginx', 'backend', 'redis'")] string name)
        {
            return await _kubePlugin.GetKubePodsAsync(AKSClusterResourceId, _namespace, kind, name);
        }

        [KernelFunction("rollout_restart_deployment")]
        [Description(
@"Restart a deployment in the specified namespace.
Used whenever user wants to restart or rollout restart a deployment, it can also be used by restart pod if the pod belongs to the deployment.
eg: restart the 'nginx-deployment' in the 'default' namespace.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> RolloutRestartDeploymentAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId,
              [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
             [Description($"Name of the Kubernetes deployment, e.g. 'nginx', 'backend'")] string deploymentName)
        {
            return await _kubePlugin.RolloutRestartDeploymentAsync(AKSClusterResourceId, _namespace, deploymentName);
        }

        [KernelFunction("scale_deployment")]
        [Description(
@"Scale a deployment in the specified namespace.
Used whenever user wants to scale a deployment, it can also be used by scale pod if the pod belongs to the deployment.
eg: scale the 'nginx-deployment' in the 'default' namespace to 3 replicas.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        [RequiresApproval]
        public async Task<string> ScaleDeploymentAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId,
              [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
             [Description($"Name of the Kubernetes deployment, e.g. 'nginx', 'backend'")] string deploymentName,
             [Description($"Number of replicas to scale to, e.g. 3")] int replicas)
        {
            return await _kubePlugin.ScaleDeploymentAsync(AKSClusterResourceId, _namespace, deploymentName, replicas);
        }

        [KernelFunction("get_kube_pod_logs")]
        [Description(
@"Get the logs of a pod in the specified namespace.
Used whenever user wants to check the logs of a specific pod.
eg: show me the last 100 lines of logs from pod 'nginx-pod-xyz' in the 'default' namespace.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> GetKubePodLogsAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId,
            [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
            [Description($"Name of the Kubernetes pod, e.g. 'backend-947df49ff-l2zb4'")] string pod,
            [Description($"Container Name inside the Kubernetes pod, e.g. 'main'. The value can be optional or empty")] string container = "",
            [Description($"Line of the logs to be print out from pods containers, it's optional, if not specified, default value is 100 lines.")] int lines = 100)
        {
            return await _kubePlugin.GetKubePodLogsAsync(AKSClusterResourceId, _namespace, pod, container, lines);
        }

        [KernelFunction("exec_command_in_pod")]
        [Description(
@"Execute a command in a pod in the specified namespace.
Used whenever user wants to run a command inside a specific pod.
eg: run 'ls -l' in pod 'nginx-pod-xyz' in the 'default' namespace.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> ExecCommandInPodAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId,
                   [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
             [Description($"Name of the Kubernetes pod, e.g. 'backend-947df49ff-l2zb4'")] string pod,
             [Description($"Container Name inside the Kubernetes pod, e.g. 'main'. The value can be optional or empty")] string? container,
             [Description($"Command to be executed inside a pod, e.g. 'ls -l', 'top'")] string command)
        {
            return await _kubePlugin.ExecCommandInPodAsync(AKSClusterResourceId, _namespace, pod, container, command);
        }

        [KernelFunction("list_crds")]
        [Description(
@"List all Custom Resource Definitions (CRDs) in the cluster.
Used whenever user wants to check what custom resources are available in the cluster.
eg: show me all CRDs in the cluster")]
        public async Task<string> ListCRDsAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId
        )
        {
            return await _kubePlugin.ListCRDsAsync(AKSClusterResourceId);
        }

        [KernelFunction("list_custom_resources")]
        [Description(
@"List custom resource objects in a namespace with specific API group and kind.
Used whenever user wants to list custom resource objects like Istio VirtualServices, ArgoCD Applications, etc.
eg: list all VirtualServices in the 'istio-system' namespace.")]
        public async Task<string> ListCustomResourcesAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId,
    [Description($"Kubernetes namespace, e.g. 'default', 'istio-system'")] string _namespace,
    [Description($"API Group of the custom resource, e.g. 'networking.istio.io'")] string apiGroup,
    [Description($"Kind of the custom resource, e.g. 'VirtualService'")] string kind)
        {
            return await _kubePlugin.ListCustomResourcesAsync(AKSClusterResourceId, _namespace, apiGroup, kind);
        }

        [KernelFunction("GetKubeResourceEvents")]
        [Description(
        @"Get the events of of a Kubernetes Deployment, StatefulSet, DaemonSet, Pod, Service or Custom Resource Object (CRD) in the specified namespace.
Used whenever user wants to check the events or history of a specific resource object.
eg: show me the events of the pod 'nginx-pod-xyz' in the 'default' namespace.")]
        public async Task<string> GetKubeResourceEventsAsync(
                    [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId,
            [Description($"Kubernetes namespace, e.g. 'default', 'istio-system'")] string _namespace,
            [Description($"API Group of the Kubernetes resource, e.g. 'apps/v1'")] string apiGroup,
            [Description($"Kind of the Kubernetes resource, e.g. 'Deployment'")] string kind,
            [Description($"Name of the Kubernetes resource")] string name)
        {
            return await _kubePlugin.GetKubeResourceEventsAsync(AKSClusterResourceId, _namespace, apiGroup, kind, name);
        }

        [KernelFunction("GetKubeResourceSpecStatus")]
        [Description(
@"Get the YAML spec and status of a Kubernetes Deployment, StatefulSet, DaemonSet, Pod, Service or Custom Resource Object (CRD) in the specified namespace.
eg: show me the YAML spec and status of 'my-service' deployment in the 'default' namespace.")]
        public async Task<string> GetKubeResourceSpecStatusAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId,
            [Description($"Kubernetes namespace, e.g. 'default', 'istio-system'")] string _namespace,
            [Description($"API Group of the Kubernetes resource, e.g. 'apps/v1'")] string apiGroup,
            [Description($"Kind of the Kubernetes resource, e.g. 'Deployment'")] string kind,
            [Description($"Name of the Kubernetes resource")] string name)
        {
            return await _kubePlugin.GetKubeResourceSpecStatusAsync(AKSClusterResourceId, _namespace, apiGroup, kind, name);
        }

        [KernelFunction("get_recently_updated_workloads")]
        [Description(
@"Get a list of Kubernetes workloads (Deployments, StatefulSets) that were updated within a specified time frame.
Used to monitor recent changes or identify workloads that might be related to recent issues.
eg: show me all workloads updated in the last 15 minutes.")]
        public async Task<string> GetRecentlyUpdatedWorkloadsAsync(
    [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId,
    [Description("Kubernetes namespace where the deployment or statefulset is located")] string _namespace,
    [Description("Number of minutes to look back for updates")] int minutesAgo)
        {
            return await _kubePlugin.GetRecentlyUpdatedWorkloadsAsync(AKSClusterResourceId, _namespace, minutesAgo);
        }

        [KernelFunction("GetKubeStatefulsets")]
        [Description(
@"Get all StatefulSets in the specified namespace. StatefulSets are used for stateful applications in Kubernetes.
Used whenever user wants to list StatefulSets in a specific namespace. eg: list all StatefulSets in the 'default' namespace.
It can also be invoked multiple times to list StatefulSets in different namespaces. eg: list all StatefulSets in the 'default' and 'kube-system' namespaces.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> GetKubeStatefulsetsAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId,
              [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace)
        {
            return await _kubePlugin.GetKubeStatefulsetsAsync(AKSClusterResourceId, _namespace);
        }


        [KernelFunction("ScaleStatefulSet")]
        [Description(
@"Scale a StatefulSet in the specified namespace.
Used whenever user wants to scale a StatefulSet, it can also be used to scale pods that belong to a StatefulSet.
eg: scale the 'redis' StatefulSet in the 'default' namespace to 3 replicas.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        [RequiresApproval]
        public async Task<string> ScaleStatefulSetAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId,
              [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
             [Description($"Name of the Kubernetes StatefulSet, e.g. 'redis', 'mongodb'")] string statefulSetName,
             [Description($"Number of replicas to scale to, e.g. 3")] int replicas)
        {
            return await _kubePlugin.ScaleStatefulSetAsync(AKSClusterResourceId, _namespace, statefulSetName, replicas);
        }

        [KernelFunction("GetAPIServerStatus")]
        [Description(
@"Get the status of the apiserver for the AKS cluster.
Used whenever user wants to check the apiserver status of the AKS cluster. Apiserver is the main component of Kubernetes control plane.
eg: show me the status of apiserver")]
        public async Task<string> GetAPIServerStatusAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId,
            [Description("Time range for checking status, e.g. '5m', '1h', '2d'")] string timeRange = "5m")
        {
            return await _kubePlugin.GetAPIServerStatusAsync(AKSClusterResourceId, timeRange);
        }

        [KernelFunction("GetEtcdStatus")]
        [Description(
@"Get the status of the etcd for the AKS cluster. 
Used whenever user wants to check the etcd status of the AKS cluster. Etcd is the key-value store used by Kubernetes to store all cluster data which is the main component of Kubernetes control plane.
eg: show me the status of etcd")]
        public async Task<string> GetEtcdStatusAsync(
    [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId,
    [Description("Time range for checking status, e.g. '5m', '1h', '2d'")] string timeRange = "5m")
        {
            return await _kubePlugin.GetEtcdStatusAsync(AKSClusterResourceId, timeRange);
        }

        [KernelFunction("DiagnoseAKSApp")]
        [Description(
@"Used to diagnose an AKS application (deployment or statefulset resource) in the specified AKS namespace to get all detailed information belong to the resource. 
It will first get all spec, status, and events of the resource, then get all pods belong to the resource.
For each pod, it will pod spec, status, events, logs, CPU/Memory metrics to this pod.
e.g.: diagnose the 'nginx' deployment in the 'default' namespace.
e.g.: check what's wrong with my 'redis' statefulset in the 'databse-system' namespace.
")]
        public async Task<string> DiagnoseAKSAppAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId,
            [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
            [Description($"Kubernetes resource kind, e.g. 'deployment', 'statefulset'")] string kind,
            [Description($"Name of the Kubernetes resource, e.g. 'nginx', 'backend', 'redis'")] string name)
        {
            return await _kubePlugin.DiagnoseAKSAppAsync(AKSClusterResourceId, _namespace, kind, name);
        }

        [KernelFunction("ApplyKubernetesYaml")]
        [Description(
        @"Applies one Kubernetes YAML object to the specified AKS cluster using server-side apply.
Used whenever user wants to create or update resources in a Kubernetes cluster using YAML.
eg: please apply this YAML object to my AKS cluster to create a new deployment.
eg: update my service with this YAML manifest.")]
        public async Task<string> ApplyKubernetesYamlAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId,
            [Description("The YAML manifest content to apply to the cluster")] string yamlContent)
        {
            return await _kubePlugin.ApplyKubernetesYamlAsync(AKSClusterResourceId, yamlContent);
        }


        [KernelFunction("GetKubeResourceMetricsRangeAsync")]
        [Description(
        @"Get the value of specific metric for Kubernetes Workload during a time range.
The supported metrics include cpu, memory.
The supported workload include deployment, statefulset, pod.
eg: please give me the cpu usage rate for deployment flask from 2023-03-01T20:10:30.781Z to 2023-03-20T20:10:30.781Z.")]
        public async Task<string> GetKubeResourceMetricsRangeAsync(
            [Description("The resource ID of the Azure Kubernetes Service.")] string AKSClusterResourceId,
            [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
            [Description($"Kubernetes resource kind, e.g. 'deployment', 'statefulset'")] string kind,
            [Description($"Name of the Kubernetes resource, e.g. 'nginx', 'backend', 'redis'")] string name,
            [Description($"Metric type, e.g. 'cpu', 'memory'")] string metricsType,
            [Description($"Start time of time range, e.g. '2023-03-01T20:10:30.781Z'")] string startTime,
            [Description($"End time of time range, e.g. '2023-03-20T20:10:30.781Z'")] string endTime,
            [Description($"Query resolution step width in time range, e.g. '5m', '1h', '2d'")] string step = "2m")
        {
            return await _kubePlugin.GetKubeResourceMetricsRangeAsync(AKSClusterResourceId, _namespace, kind, name, metricsType, step, startTime, endTime);
        }
    }
}
