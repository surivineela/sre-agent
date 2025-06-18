// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Attributes;
using Agent.Framework;
using Agent.Plugins.Helpers;
using Agent.Plugins.Interface;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    [AgentToolPlugin]
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
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId)
        {
            return await _kubePlugin.GetKubeNamespacesAsync(AKSClusterResourceId);
        }

        [KernelFunction("get_kube_pods")]
        [Description(
@"Get all pods belong to the specific resource and namespace.
Used whenever user wants to list pods in a specific deployment or statefulset. eg: list all pods in the 'nginx-deployment' in the 'default' namespace.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> GetKubePodsAsync(
             [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
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
        [RequiresApproval("Requires approval to rollout restart a deployment.", useOboToken: false)]
        public async Task<string> RolloutRestartDeploymentAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
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
        [RequiresApproval("Requires approval to scale a deployment.", useOboToken: false)]
        [WriteAction(runInReadOnlyMode: true)]
        public async Task<string> ScaleDeploymentAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
              [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
             [Description($"Name of the Kubernetes deployment, e.g. 'nginx', 'backend'")] string deploymentName,
             [Description($"Number of replicas to scale to, e.g. 3")] int replicas,
             String agentmode)
        {
            return await _kubePlugin.ScaleDeploymentAsync(AKSClusterResourceId, _namespace, deploymentName, replicas, agentmode);
        }

        [KernelFunction("get_kube_pod_logs")]
        [Description(
@"Get the logs of a pod in the specified namespace.
Used whenever user wants to check the logs of a specific pod.
eg: show me the last 100 lines of logs from pod 'nginx-pod-xyz' in the 'default' namespace.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> GetKubePodLogsAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
            [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
            [Description($"Name of the Kubernetes pod, e.g. 'backend-947df49ff-l2zb4'")] string pod,
            [Description($"Container Name inside the Kubernetes pod, e.g. 'main'. The value can be optional or empty")] string container = "",
            [Description($"Line of the logs to be print out from pods containers, it's optional, if not specified, default value is 100 lines.")] int lines = 100)
        {
            return await _kubePlugin.GetKubePodLogsAsync(AKSClusterResourceId, _namespace, pod, container, lines);
        }

        [KernelFunction("list_crds")]
        [Description(
@"List all Custom Resource Definitions (CRDs) in the cluster.
Used whenever user wants to check what custom resources are available in the cluster.
eg: show me all CRDs in the cluster")]
        public async Task<string> ListCRDsAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId
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
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
    [Description($"Kubernetes namespace, e.g. 'default', 'istio-system'")] string _namespace,
    [Description($"API Group of the custom resource, e.g. 'networking.istio.io'")] string apiGroup,
    [Description($"Kind of the custom resource, e.g. 'VirtualService'")] string kind)
        {
            return await _kubePlugin.ListCustomResourcesAsync(AKSClusterResourceId, _namespace, apiGroup, kind);
        }

        [KernelFunction("GetKubeResourceEvents")]
        [Description(
@"Get the events of a Kubernetes resource (Deployment, StatefulSet, DaemonSet, Pod, Service, Node, PV, or Custom Resource Object) by name.
Used whenever user wants to check the events or history of a specific resource object.
eg: show me the events of the pod 'nginx-pod-xyz' in the 'default' namespace.")]
        public async Task<string> GetKubeResourceEventsAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
            [Description($"API Group of the Kubernetes resource, e.g. 'apps/v1'")] string apiGroup,
            [Description($"Kind of the Kubernetes resource, e.g. 'Deployment'")] string kind,
            [Description($"Name of the Kubernetes resource")] string name,
            [Description($"Kubernetes namespace, e.g. 'default', 'istio-system'. Optional for cluster-scoped kinds (Node, PersistentVolume); required for namespaced kinds.")] string? _namespace = "")
        {
            return await _kubePlugin.GetKubeResourceEventsAsync(AKSClusterResourceId, _namespace, apiGroup, kind, name);
        }

        [KernelFunction("GetKubeResourceSpecStatus")]
        [Description(
@"Get the YAML spec and status of a Kubernetes resource (Deployment, StatefulSet, DaemonSet, Pod, Service, Node, PV, PVC, or Custom Resource Object) by name.
e.g. show me the YAML spec and status of 'my-service' deployment in the 'default' namespace.
e.g. get spec for node aks-nodepool1-12345678-vmss000000.")]
        public async Task<string> GetKubeResourceSpecStatusAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
            [Description($"API Group of the Kubernetes resource, e.g. 'apps/v1'")] string apiGroup,
            [Description($"Kind of the Kubernetes resource, e.g. 'Deployment'")] string kind,
            [Description($"Name of the Kubernetes resource")] string name,
            [Description($"Kubernetes namespace, e.g. 'default', 'istio-system'. Optional for cluster-scoped kinds (Node, PersistentVolume); required for namespaced kinds.")] string? _namespace = "")
        {
            return await _kubePlugin.GetKubeResourceSpecStatusAsync(AKSClusterResourceId, _namespace, apiGroup, kind, name);
        }

        [KernelFunction("get_recently_updated_workloads")]
        [Description(
@"Get a list of Kubernetes workloads (Deployments, StatefulSets) that were updated within a specified time frame.
Used to monitor recent changes or identify workloads that might be related to recent issues.
eg: show me all workloads updated in the last 15 minutes.")]
        public async Task<string> GetRecentlyUpdatedWorkloadsAsync(
    [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
    [Description("Kubernetes namespace where the deployment or statefulset is located")] string _namespace,
    [Description("Number of minutes to look back for updates")] int minutesAgo)
        {
            return await _kubePlugin.GetRecentlyUpdatedWorkloadsAsync(AKSClusterResourceId, _namespace, minutesAgo);
        }

        [KernelFunction("ListKubeResources")]
        [Description(
@"Get all Kubernetes resources in the specified namespace with specified kind.
Supported kinds include Deployment, Service, Statefulset, Pod, Job, Configmap, Secret, Ingress, ReplicaSet, Daemonset, and Node.
e.g., 'list all deployments in the default namespace', 'list all nodes'.
It can also be invoked multiple times to list deployments in different namespaces. eg: list all deployments in the 'default' and 'kube-system' namespaces.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        public async Task<string> ListKubeResourcesAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
            [Description($"Kubernetes resource kind, e.g. 'deployment', 'statefulset', 'service'")] string kind,
            [Description($"Kubernetes namespace, e.g. 'default', 'istio-system'. Optional for cluster-scoped kinds (Node); required for namespaced kinds.")] string? _namespace = "")
        {
            return await _kubePlugin.ListKubeResourcesAsync(AKSClusterResourceId, _namespace, kind);
        }


        [KernelFunction("ScaleStatefulSet")]
        [Description(
@"Scale a StatefulSet in the specified namespace.
Used whenever user wants to scale a StatefulSet, it can also be used to scale pods that belong to a StatefulSet.
eg: scale the 'redis' StatefulSet in the 'default' namespace to 3 replicas.
If user didn't specify namespace in the context, try to use 'default' namespace")]
        [RequiresApproval("Requires approval to scale a StatefulSet.", useOboToken: false)]
        public async Task<string> ScaleStatefulSetAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
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
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
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
    [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
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
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
            [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
            [Description($"Kubernetes resource kind, e.g. 'deployment', 'statefulset'")] string kind,
            [Description($"Name of the Kubernetes resource, e.g. 'nginx', 'backend', 'redis'")] string name)
        {
            return await _kubePlugin.DiagnoseAKSAppAsync(AKSClusterResourceId, _namespace, kind, name);
        }

        [KernelFunction("PatchKubernetesYaml")]
        [Description(
        @"Applies one Kubernetes YAML object to the specified AKS cluster using server-side apply.
        When patch for array values, make sure all existing values are included in the YAML object.
Used whenever user wants to create or update resources in a Kubernetes cluster using YAML.
eg: please apply this YAML object to my AKS cluster to create a new deployment.
eg: update my service with this YAML manifest.")]
        [RequiresApproval("Requires approval to apply Kubernetes YAML.", useOboToken: false)]
        public async Task<string> PatchKubernetesYamlAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
            [Description("The YAML manifest content to apply to the cluster")] string yamlContent)
        {
            return await _kubePlugin.PatchKubernetesYamlAsync(AKSClusterResourceId, yamlContent);
        }


        [KernelFunction("GetKubeResourceMetricsRangeAsync")]
        [Description(
@"Get the value of specific metric for Kubernetes Workload during a time range.
The supported metrics include cpu, memory, availability percentage.
The supported workload include deployment, statefulset, pod, and node.
eg: please give me the cpu usage rate for deployment flask from 2023-03-01T20:10:30.781Z to 2023-03-20T20:10:30.781Z.
eg: please give me the memory usage rate for deployment checkout for last 1 hour.
eg: please give me the availability rate for statefulset for last 2 hour.")]
        public async Task<string> GetKubeResourceMetricsRangeAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
            [Description($"Kubernetes resource kind, e.g. 'deployment', 'statefulset'")] string kind,
            [Description($"Name of the Kubernetes resource, e.g. 'nginx', 'backend', 'redis'")] string name,
            [Description($"Metric type, e.g. 'cpu', 'memory'")] string metricsType,
            [Description($"Start time of time range, e.g. '2023-03-01T20:10:30.781Z'")] string startTime,
            [Description($"End time of time range, e.g. '2023-03-20T20:10:30.781Z'")] string endTime,
            [Description($"Kubernetes namespace, e.g. 'default', 'istio-system'. Optional for cluster-scoped kinds (Node); required for namespaced kinds.")] string? _namespace)
        {
            return await _kubePlugin.GetKubeResourceMetricsRangeAsync(AKSClusterResourceId, _namespace, kind, name, metricsType, startTime, endTime);
        }

        [KernelFunction("listWorkloadRevisions")]
        [Description(
@"List all revisions for a specific Kubernetes workload (Deployment or StatefulSet) and sort by revision number.
For deployments, it fetches ReplicaSets owned by the deployment.
For StatefulSets, it fetches ControllerRevision objects.
Used whenever user wants to check the revision history of a workload.
eg: show me all revisions of the 'nginx' deployment in the 'default' namespace.")]
        public async Task<string> ListWorkloadRevisionsAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
            [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
            [Description($"Kubernetes workload kind, e.g. 'deployment', 'statefulset'")] string kind,
            [Description($"Name of the Kubernetes workload, e.g. 'nginx', 'backend', 'redis'")] string name)
        {
            return await _kubePlugin.ListWorkloadRevisions(AKSClusterResourceId, _namespace, kind, name);
        }

        [KernelFunction("runKubectlReadCommand")]
        [Description("""
        Safely execute kubectl commands to retrieve Kubernetes resource information. Several subcommands are supported, including 'get', 'describe', 'logs', 'top', 'api-resources', and 'api-versions'.
        USAGE: Provide the complete kubectl command as a string.
        BASIC EXAMPLES:
        - Specific namespace: 'kubectl get pods -n production -o name'
        - Describe a resource: 'kubectl describe pod my-pod -n default'
        - Get logs from a pod: 'kubectl logs my-pod -n default --container my-container --tail 100'
        ADVANCED EXAMPLES:
        - Complete security info: 'kubectl get pods -o custom-columns=NAME:.metadata.name,NAMESPACE:.metadata.namespace,PRIVILEGED:.spec.containers[*].securityContext.privileged,HOST_NETWORK:.spec.hostNetwork,HOST_PID:.spec.hostPID,CAPABILITIES:.spec.containers[*].securityContext.capabilities.add'
        BEST PRACTICES:
        - Always specify the namespace you care about: 'kubectl get pods -n default'
        """)]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> RunKubectlReadCommandAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
            [Description($"Complete kubectl get command string, e.g.: 'kubectl get deployments -n production -o wide'")] string command)
        {
            return await _kubePlugin.RunKubectlReadCommandAsync(AKSClusterResourceId, command);
        }

        [KernelFunction("runKubectlWriteCommand")]
        [Description("""
        Safely execute kubectl commands to update/create/delete Kubernetes resource. Several subcommands are supported, including 'create', 'apply', 'delete', 'patch', 'replace', 'scale', 'rollout', 'label' and 'annotate'.
        USAGE: Provide the complete kubectl command as a string.
        BASIC EXAMPLES:
        - Create a deployment: 'kubectl create deployment my-deployment --image=my-image -n production'
        - Apply a configuration: 'kubectl apply -f my-config.yaml -n default'
        - Delete a pod: 'kubectl delete pod my-pod -n default'
        - Scale a deployment: 'kubectl scale deployment my-deployment --replicas=3 -n production'
        - Rollout restart a deployment: 'kubectl rollout restart deployment my-deployment -n default'
        - Patch a resource: 'kubectl patch deployment my-deployment -p \"{\"spec\":{\"replicas\":3}}\" -n default'
        - Label a resource: 'kubectl label pod my-pod my-label=my-value -n default'
        BEST PRACTICES:
        - Always specify the namespace you care about: 'kubectl get pods -n default'
        """)]
        [WriteAction]
        public async Task<string> RunKubectlWriteCommandAsync(
             [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
             [Description($"Complete kubectl get command string, e.g.: 'kubectl get deployments -n production -o wide'")] string command,
             [Description("For command that requires STDIN, use this parameter to pass STDIN content, e.g.: kubectl apply -f - <stdin>")] string stdin = "")
        {
            return await _kubePlugin.RunKubectlWriteCommandAsync(AKSClusterResourceId, command, stdin);
        }

        [Description(
            @"Provides help information about kubectl commands and resources.
            Used whenever user needs guidance on using kubectl commands or understanding Kubernetes resources.
            eg: 'How do I use kubectl get pods?', 'What options are available for kubectl describe?'.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> RunKubectlCommandHelpAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")] string AKSClusterResourceId,
            [Description("Expected kubectl command input format example: 'get', 'describe pod', 'create deployment', etc. The full command will be composed with 'kubectl' prefix and ''--help' suffix.")] string command)
        {
            return await _kubePlugin.RunKubectlCommandHelpAsync(AKSClusterResourceId, command);
        }

        /// <summary>Run 'kubectl get' on any resource.</summary>
        [Description(
            "Retrieve Kubernetes resources with optional label filtering and custom columns.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> KubectlGetAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")]
            string AKSClusterResourceId,
            [Description("Resource kind, e.g. pods | svc | ingress | pvc | pv | nodes | crds")]
            string kind,
            // "namespace" is a keyword, so we use @namespace
            [Description("Namespace name. Use '*' for all namespaces. " +
                     "Leave empty for cluster‑scoped kinds (nodes, pv, crd, namespace, etc.).")]
            string? @namespace = "*",
            [Description("Optional label selector, e.g. '<label-key>=<label-value>'. Omit for none. Used ONLY when label selector already known from some resources, such as 'spec.selector' of Service to select pods.")]
            string? selector = null,
            [Description("Columns to output, each as <LABEL>:<jsonpath>. " +
                     "Example: NAME:.metadata.name,STATUS:.status.phase")]
            string columnsCsv = "NAME:.metadata.name")
        {
            if (string.Equals(kind, "event", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "events", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult($"Unsupported resource kind: {kind}. Use {nameof(GetKubeEventsAsync)} instead.");
            }

            // Build command ----------------------------------------------------
            var args = new List<string> { "kubectl", "get", kind };
            if (!string.IsNullOrWhiteSpace(@namespace) && @namespace != "*")
            {
                args.AddRange(["-n", @namespace]);
            }
            else if (@namespace == "*")
            {
                args.Add("-A");
            }

            if (!string.IsNullOrWhiteSpace(selector))
            {
                args.Add("--selector=" + selector);
            }

            args.Add($"-o custom-columns={columnsCsv}");
            var command = string.Join(' ', args);

            return _kubePlugin.RunKubectlReadCommandAsync(AKSClusterResourceId, command);
        }

        /// <summary>
        /// Describe a single Kubernetes object (human‑readable detail + events).
        /// </summary>
        [Description(
            "Run 'kubectl describe' on a single object. " +
            "Must specify kind, name, and namespace (or empty for cluster‑scoped kinds).")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> KubectlDescribeAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")]
            string AKSClusterResourceId,
            [Description("Resource kind, e.g. pod, svc, ingress, pvc, node, crd")]
            string kind,
            [Description("Object name")]
            string name,
            [Description("Namespace. Use '*' for all namespaces (rare). " +
            "Leave empty for cluster‑scoped kinds like node or crd.")]
            string? @namespace = "default")
        {
            var args = new List<string> { "kubectl", "describe", kind, name };

            if (!string.IsNullOrWhiteSpace(@namespace) && @namespace != "*")
                args.AddRange(["-n", @namespace]);
            else if (@namespace == "*")
                args.Add("-A");

            var command = string.Join(' ', args);

            return _kubePlugin.RunKubectlReadCommandAsync(AKSClusterResourceId, command);
        }

        /// <summary>
        /// Explain fields of a resource schema (kubectl explain).
        /// </summary>
        [Description(
            "Run 'kubectl explain' for API documentation. " +
            "Always specify full resourcePath (e.g. 'pod.spec.containers') and " +
            "whether recursion is desired.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> KubectlExplainAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")]
            string AKSClusterResourceId,
            [Description("Resource path, e.g. 'pod.spec', 'deployment.spec.strategy'")]
            string resourcePath,
            [Description("If true, include all nested fields (-R / --recursive).")]
            bool recursive = false,
            [Description("Optional apiVersion such as 'apps/v1'. Omit for default.")]
            string? apiVersion = null)
        {
            var args = new List<string> { "kubectl", "explain", resourcePath };

            if (recursive) args.Add("--recursive");
            if (!string.IsNullOrWhiteSpace(apiVersion))
                args.Add("--api-version=" + apiVersion);

            var command = string.Join(' ', args);

            return _kubePlugin.RunKubectlReadCommandAsync(AKSClusterResourceId, command);
        }

        /// <summary>
        /// List every API resource the cluster supports (kubectl api-resources).
        /// </summary>
        [Description(
            "Run 'kubectl api-resources' with optional filters and explicit output columns.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> KubeApiResourcesAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")]
            string AKSClusterResourceId,
            [Description("Filter by namespaced flag: true | false | omit")]
            string? namespaced = null,
            [Description("Filter by API group, e.g. 'apps' or 'batch'. Omit for all groups.")]
            string? apiGroup = null)
        {
            var args = new List<string> { "kubectl", "api-resources" };

            if (!string.IsNullOrWhiteSpace(namespaced))
                args.Add($"--namespaced={namespaced}");

            if (!string.IsNullOrWhiteSpace(apiGroup))
                args.Add($"--api-group={apiGroup}");

            args.Add($"-o wide");

            var command = string.Join(' ', args);

            return _kubePlugin.RunKubectlReadCommandAsync(AKSClusterResourceId, command);
        }

        /// <summary>Get pod logs with advanced filtering and volume reduction.</summary>
        [Description(
            "Retrieve Kubernetes pod logs with grep filtering, truncation, and all built-in kubectl log options.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetPodLogsAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")]
            string AKSClusterResourceId,
            [Description("Pod name or resource/name (e.g. 'mypod' or 'deployment/myapp')")]
            string podOrResource,
            [Description("Namespace name. Leave empty for current namespace context.")]
            string? @namespace = null,
            [Description("Container name. Leave empty for single-container pods or first container.")]
            string? container = null,
            [Description("Search terms for grep filtering. Space-separated for OR, comma-separated for AND. " +
                 "Example: 'error warn' (OR) or 'error,database' (AND)")]
            string? grepTerms = null,
            [Description("Case-sensitive grep search. Default: false (case-insensitive)")]
            bool caseSensitive = false,
            [Description("Number of tail lines to retrieve. Default: 100. Use -1 for all logs.")]
            int tailLines = 100,
            [Description("Time duration to look back (e.g. '1h', '30m', '24h'). Overrides tailLines if specified.")]
            string? since = null,
            [Description("Include timestamps in output. Default: true")]
            bool timestamps = true,
            [Description("Get logs from previous terminated container. Default: false")]
            bool previous = false,
            [Description("Get logs from all containers in the pod. Default: false")]
            bool allContainers = false,
            [Description("Show prefix with pod/container name. Default: false")]
            bool showPrefix = false)
        {
            try
            {
                // Build kubectl logs command
                var args = new List<string> { "kubectl", "logs", podOrResource };

                if (!string.IsNullOrWhiteSpace(@namespace))
                    args.AddRange(["-n", @namespace]);

                if (!string.IsNullOrWhiteSpace(container))
                    args.AddRange(["-c", container]);

                if (!string.IsNullOrWhiteSpace(since))
                    args.Add($"--since={since}");
                else if (tailLines > 0)
                    args.Add($"--tail={tailLines}");

                if (timestamps)
                    args.Add("--timestamps");

                if (previous)
                    args.Add("--previous");

                if (allContainers)
                    args.Add("--all-containers");

                if (showPrefix)
                    args.Add("--prefix");

                // Execute kubectl command
                var command = string.Join(' ', args);
                var rawLogs = await _kubePlugin.RunKubectlReadCommandAsync(AKSClusterResourceId, command);

                if (rawLogs.StartsWith("kubectl command failed"))
                {
                    return rawLogs;
                }

                // Apply grep filtering
                var filteredLogs = KernelFunctionHelpers.ApplyGrepFiltering(rawLogs, grepTerms, caseSensitive);

                // Apply word truncation
                var finalLogs = TextVolumeHelpers.ApplyWordTruncation(filteredLogs);

                return finalLogs;
            }
            catch (Exception ex)
            {
                return $"Failed to get logs: {ex.Message}";
            }
        }

        /// <summary>Get Kubernetes events with filtering and volume reduction.</summary>
        [Description(
            "Retrieve Kubernetes events with grep filtering, truncation, and built-in event filtering options.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetKubeEventsAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")]
            string AKSClusterResourceId,
            [Description("Namespace name. Use '*' for all namespaces. Leave empty for current namespace context.")]
            string? @namespace = "*",
            [Description("Field selector for filtering events (e.g. 'involvedObject.name=mypod' or 'type=Warning')")]
            string? fieldSelector = null,
            [Description("Search terms for grep filtering. Space-separated for OR, comma-separated for AND. " +
                 "Example: 'failed error' (OR) or 'failed,pulling' (AND)")]
            string? grepTerms = null,
            [Description("Case-sensitive grep search. Default: false (case-insensitive)")]
            bool caseSensitive = false,
            [Description("Sort events by field (e.g. '.lastTimestamp', '.metadata.name'). Default: '.lastTimestamp'")]
            string sortBy = ".lastTimestamp",
            [Description("Event types to include: 'All', 'Normal', 'Warning'. Default: 'All'")]
            string eventTypes = "All")
        {
            try
            {
                // Build kubectl get events command
                var args = new List<string> { "get", "events" };

                if (!string.IsNullOrWhiteSpace(@namespace) && @namespace != "*")
                    args.AddRange(["-n", @namespace]);
                else if (@namespace == "*")
                    args.Add("-A");

                if (!string.IsNullOrWhiteSpace(fieldSelector))
                    args.Add($"--field-selector={fieldSelector}");

                // Add event type filtering
                if (eventTypes != "All")
                {
                    var typeFilter = eventTypes == "Warning" ? "type=Warning" : "type=Normal";
                    var existingSelector = fieldSelector ?? "";
                    var combinedSelector = string.IsNullOrEmpty(existingSelector)
                        ? typeFilter
                        : $"{existingSelector},{typeFilter}";

                    // Remove previous field-selector and add combined one
                    args.RemoveAll(arg => arg.StartsWith("--field-selector="));
                    args.Add($"--field-selector={combinedSelector}");
                }

                args.Add($"--sort-by={sortBy}");
                args.Add("-o wide");  // Get more details

                // Execute kubectl command
                var command = string.Join(' ', args);
                var rawEvents = await _kubePlugin.RunKubectlReadCommandAsync(AKSClusterResourceId, command);

                if (rawEvents.StartsWith("kubectl command failed"))
                {
                    return rawEvents;
                }

                // Apply limit (take header + first N events)
                var limitedEvents = KernelFunctionHelpers.ApplyEventLimit(rawEvents);

                // Apply grep filtering
                var filteredEvents = KernelFunctionHelpers.ApplyGrepFiltering(limitedEvents, grepTerms, caseSensitive);

                // Apply word truncation
                var finalEvents = TextVolumeHelpers.ApplyWordTruncation(filteredEvents);

                return finalEvents;
            }
            catch (Exception ex)
            {
                return $"Failed to get events: {ex.Message}";
            }
        }

        /// <summary>Get available metrics with filtering to discover what's available.</summary>
        [Description(
            "Discover available Prometheus metrics with optional filtering.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> DiscoverPrometheusMetricsAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")]
            string AKSClusterResourceId,
            [Description("Filter metrics by name pattern (supports wildcards). Example: 'container_*' or '*memory*'")]
            string? namePattern = null,
            [Description("Filter by metric type: counter, gauge, histogram, summary")]
            string? metricType = null)
        {
            return _kubePlugin.DiscoverMetricsAsync(
                AKSClusterResourceId,
                namePattern,
                metricType);
        }

        /// <summary>Get metric labels and values for building targeted queries.</summary>
        [Description(
            "Discover available label names and values for a specific metric to build more targeted queries.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> GetMetricsLabelsAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")]
            string AKSClusterResourceId,
            [Description("Metric name to inspect, e.g. 'container_memory_usage_bytes'")]
            string metricName,
            [Description("Specific label name to get values for. Leave empty to get all label names.")]
            string? labelName = null)
        {
            return _kubePlugin.GetMetricLabelsAsync(
                AKSClusterResourceId,
                metricName,
                labelName);
        }

        /// <summary>Execute PromQL queries against Prometheus with volume controls.</summary>
        [Description(
            "Query Prometheus metrics with comprehensive filtering and aggregation options to control output volume.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> QueryPrometheusMetricsAsync(
            [Description("The resource ID of the Azure Kubernetes Service. e.g. '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.ContainerService/managedClusters/{cluster-name}'")]
            string AKSClusterResourceId,
            [Description("PromQL query expression, e.g. 'cpu_usage_rate' or 'container_memory_usage_bytes{namespace=\"default\"}'")]
            string query,
            [Description("Time range duration (e.g. '1h', '30m', '24h') or 'now' for instant query. Default: '1h'")]
            string duration = "1h",
            [Description("Query resolution/step interval (e.g. '30s', '1m', '5m'). Larger steps = less data. Default: '1m'")]
            string step = "5m",
            [Description("Additional label filters as key=value pairs, comma-separated. Example: 'namespace=default,pod=myapp-*'")]
            string? labelFilters = null,
            [Description("Aggregation function to apply: sum, avg, max, min, count, topk, bottomk. Use with aggregateBy parameter.")]
            string? aggregateFunction = null,
            [Description("Labels to aggregate by when using aggregateFunction. Example: 'namespace,pod'")]
            string? aggregateBy = null,
            [Description("Limit number of time series returned. Useful for high-cardinality metrics. Default: no limit")]
            int? limit = null,
            [Description("Only return values above this threshold. Helps filter noise from metrics.")]
            double? minValue = null)
        {
            return _kubePlugin.ExecutePromQLAsync(
                AKSClusterResourceId,
                query,
                duration,
                step,
                labelFilters,
                aggregateFunction,
                aggregateBy,
                limit,
                minValue);
        }

        [KernelFunction("profile_dotnet_app_cpu_in_aks_container")]
        [Description(
        @"Performs CPU profiling for a .NET application running in a specific pod and container.
            The analysis ('topN' report) is also performed inside the container, and its result is returned.
            Failures during tool installation or profiling will be reported in the output.
            eg: 'Profile CPU of 'my-app-pod' in 'default' for 60s.'"
        )]
        [RequiresApproval("Requires approval to execute CPU profiling tools within the specified pod and container.", useOboToken: false)]
        public async Task<string> ProfileDotnetAppCpuInAKSContainerAsync(
        [Description("The resource ID of the Azure Kubernetes Service (AKS) cluster.")] string AKSClusterResourceId,
        [Description("Kubernetes namespace where the pod is located.")] string _namespace,
        [Description("The name of the Kubernetes pod running the .NET application.")] string podName,
        [Description("Optional: The name of the specific container within the pod. Auto-selected if single container or based on heuristics for multiple.")] string? targetContainerName = null,
        [Description("Duration in seconds for which to collect the CPU trace. Default is 30 seconds.")] int durationSeconds = 30)
        {
            return await _kubePlugin.ProfileDotnetAppCpuInAKSContainerAsync(AKSClusterResourceId, _namespace, podName, targetContainerName, durationSeconds);
        }

        [KernelFunction("analyze_dotnet_app_memory_in_aks_container")]
        [Description(
    @"Performs memory analysis for a .NET application running in a specific pod and container within an AKS cluster.
    This involves collecting a memory dump, running an analyzer tool inside the container, and returning the analysis results.
    This tool can help identify memory leaks, high memory usage patterns, and other memory-related issues in .NET applications.
    Use this when investigating memory problems for a .NET app in AKS.
    eg: 'Analyze the memory of the .NET app in pod 'cart-service-pod-abc789' in the 'e-commerce' namespace.'
    eg: 'My .NET app 'order-processor' in pod 'proc-pod-123' seems to be using too much memory, can you analyze it?'"
    )]
        [RequiresApproval("Requires approval to execute memory dump collection and analysis tools within the specified pod and container. This involves running scripts and potentially installing diagnostic tools inside the container.", useOboToken: false)]
        public async Task<string> AnalyzeDotnetAppMemoryInAKSContainerAsync(
            [Description("The resource ID of the Azure Kubernetes Service (AKS) cluster.")] string AKSClusterResourceId,
            [Description("Kubernetes namespace where the pod is located.")] string _namespace,
            [Description("The name of the Kubernetes pod running the .NET application.")] string podName,
            [Description("Optional: The name of the specific container within the pod. If not provided, the plugin will attempt to select an appropriate container (e.g., the first one).")] string? targetContainerName = null)
        {
            return await _kubePlugin.AnalyzeDotnetAppMemoryInAKSContainerAsync(AKSClusterResourceId, _namespace, podName, targetContainerName);
        }
    }
}
