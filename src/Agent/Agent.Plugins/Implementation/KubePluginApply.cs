// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Logging;
using Agent.Plugins.Interface;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using YamlDotNet.Serialization;
using Constants = Agent.Graph.Crawler.ARM.Constants;

namespace Agent.Plugins
{
    public partial class KubePlugin : IKubePlugin
    {

        /// <summary>
        /// Applies a Kubernetes YAML object to the specified AKS cluster using client-side apply
        /// </summary>
        /// <param name="resourceId">The AKS cluster resource ID</param>
        /// <param name="yamlContent">The YAML content to apply</param>
        /// <returns>A string indicating the result of the operation</returns>
        public async Task<string> PatchKubernetesYamlAsync(string resourceId, string yamlContent)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return "Error: AKS Cluster Resource ID is empty or null";
            }
            if (yamlContent.Contains("---"))
            {
                return "Error parsing multiple YAML objects, only support one yaml object a time";
            }
            try
            {
                if (string.IsNullOrWhiteSpace(yamlContent))
                {
                    return "Error: YAML content is empty or null";
                }

                // Get the Kubernetes client
                var client = await GetOrCreateClientAsync(resourceId);                // Parse YAML with a single deserializer that preserves numeric types
                var yamlDeserializer = new DeserializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                    .Build();
                // Parse the provided YAML into k8sObject to extract metadata
                var yaml = new StringReader(yamlContent);
                var k8sObj = yamlDeserializer.Deserialize<k8sObject>(yaml);
                if (k8sObj == null || string.IsNullOrEmpty(k8sObj.Kind) || string.IsNullOrEmpty(k8sObj.ApiVersion))
                {
                    return "Error: Invalid YAML document: Missing 'kind' or 'apiVersion'";
                }

                // Extract metadata for resource name and namespace
                string? resourceName = k8sObj.Metadata?.Name;
                string? namespaceName = k8sObj.Metadata?.Namespace; // Default to "default" namespace if not specified

                // Also parse into dictionary for backward compatibility
                yaml = new StringReader(yamlContent);
                var yamlObject = yamlDeserializer.Deserialize<Dictionary<string, object?>>(yaml);

                string? kind = null;
                string? apiVersion = null;
                if (yamlObject["kind"] != null)
                {
                    kind = yamlObject["kind"]?.ToString();
                }

                if (yamlObject["apiVersion"] != null)
                {
                    apiVersion = yamlObject["apiVersion"]?.ToString();
                }

                if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(apiVersion))
                {
                    return "Error: Invalid YAML document: 'kind' or 'apiVersion' has null or empty value";
                }

                _logger?.LogInternalInformation("Parsing YAML object with kind: {Kind}, apiVersion: {ApiVersion}", kind, apiVersion);

                // Use strongly-typed KubernetesYaml deserialization based on resource kind
                object? k8sObject = null;
                // Switch based on resource kind to use the appropriate typed deserializer
                switch (kind?.ToLowerInvariant())
                {
                    case "deployment":
                        k8sObject = KubernetesYaml.Deserialize<V1Deployment>(yamlContent);
                        break;
                    case "service":
                        k8sObject = KubernetesYaml.Deserialize<V1Service>(yamlContent);
                        break;
                    case "ingress":
                        k8sObject = KubernetesYaml.Deserialize<V1Ingress>(yamlContent);
                        break;
                    case "configmap":
                        k8sObject = KubernetesYaml.Deserialize<V1ConfigMap>(yamlContent);
                        break;
                    case "secret":
                        k8sObject = KubernetesYaml.Deserialize<V1Secret>(yamlContent);
                        break;
                    case "statefulset":
                        k8sObject = KubernetesYaml.Deserialize<V1StatefulSet>(yamlContent);
                        break;
                    case "job":
                        k8sObject = KubernetesYaml.Deserialize<V1Job>(yamlContent);
                        break;
                    case "cronjob":
                        k8sObject = KubernetesYaml.Deserialize<V1CronJob>(yamlContent);
                        break;
                    case "daemonset":
                        k8sObject = KubernetesYaml.Deserialize<V1DaemonSet>(yamlContent);
                        break;
                    case "replicaset":
                        k8sObject = KubernetesYaml.Deserialize<V1ReplicaSet>(yamlContent);
                        break;
                    case "pod":
                        k8sObject = KubernetesYaml.Deserialize<V1Pod>(yamlContent);
                        break;
                    case "persistentvolumeclaim":
                        k8sObject = KubernetesYaml.Deserialize<V1PersistentVolumeClaim>(yamlContent);
                        break;
                    case "persistentvolume":
                        k8sObject = KubernetesYaml.Deserialize<V1PersistentVolume>(yamlContent);
                        break;
                    case "serviceaccount":
                        k8sObject = KubernetesYaml.Deserialize<V1ServiceAccount>(yamlContent);
                        break;
                    case "role":
                        k8sObject = KubernetesYaml.Deserialize<V1Role>(yamlContent);
                        break;
                    case "rolebinding":
                        k8sObject = KubernetesYaml.Deserialize<V1RoleBinding>(yamlContent);
                        break;
                    case "clusterrole":
                        k8sObject = KubernetesYaml.Deserialize<V1ClusterRole>(yamlContent);
                        break;
                    case "clusterrolebinding":
                        k8sObject = KubernetesYaml.Deserialize<V1ClusterRoleBinding>(yamlContent);
                        break;
                    case "namespace":
                        k8sObject = KubernetesYaml.Deserialize<V1Namespace>(yamlContent);
                        break;
                    case "networkpolicy":
                        k8sObject = KubernetesYaml.Deserialize<V1NetworkPolicy>(yamlContent);
                        break;
                    case "limitrange":
                        k8sObject = KubernetesYaml.Deserialize<V1LimitRange>(yamlContent);
                        break;
                    case "resourcequota":
                        k8sObject = KubernetesYaml.Deserialize<V1ResourceQuota>(yamlContent);
                        break;
                    default:
                        break;
                }

                _logger?.LogInternalInformation($"Applying resource {kind}/{resourceName} in namespace {namespaceName}");
                var jsonBody = k8sObject != null
                    ? KubernetesJson.Serialize(k8sObject)  // Use the strongly-typed object if available
                    : KubernetesJson.Serialize(yamlObject); // Fallback to the generic object
                _logger?.LogDebug("Converted YAML to JSON using k8s.KubernetesJson: {JsonBody}", jsonBody);

                // Create patch and apply it - CHANGE THIS PART
                bool resourceExists = false;

                try
                {
                    // Check if resource exists - handle cluster-scoped vs namespaced resources
                    if (await IsClusterScopedResourceAsync(client, kind, GetApiGroup(apiVersion ?? string.Empty)))
                    {
                        var existingResource = await client.CustomObjects.GetClusterCustomObjectAsync(
                            group: GetApiGroup(apiVersion ?? string.Empty),
                            version: GetApiVersion(apiVersion ?? string.Empty),
                            plural: GetPluralFormForKind(kind ?? string.Empty),
                            name: resourceName);
                        resourceExists = existingResource != null;
                    }
                    else
                    {
                        var existingResource = await client.CustomObjects.GetNamespacedCustomObjectAsync(
                            group: GetApiGroup(apiVersion ?? string.Empty),
                            version: GetApiVersion(apiVersion ?? string.Empty),
                            namespaceParameter: namespaceName,
                            plural: GetPluralFormForKind(kind ?? string.Empty),
                            name: resourceName);
                        resourceExists = existingResource != null;
                    }

                    _logger?.LogInternalInformation("Resource {Kind}/{Name} already exists, will update", kind, resourceName);
                }
                catch (k8s.Autorest.HttpOperationException ex) when (ex.Response?.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger?.LogInternalInformation("Resource {Kind}/{Name} does not exist, will create", kind, resourceName);
                    resourceExists = false;
                }
                catch (k8s.Autorest.HttpOperationException ex) when (ex.Response?.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger?.LogInternalError(ex, "Forbidden error checking resource existence for {Kind}/{Name} in cluster {ResourceId}", kind, resourceName, resourceId);
                    return await GetPermissionErrorMessageAsync(resourceId);
                }
                catch (Exception ex)
                {
                    _logger?.LogInternalError(ex, "Error checking if resource {Kind}/{Name} exists in cluster {ResourceId}", kind, resourceName, resourceId);
                    return $"Error checking resource existence: {ex.Message}";
                }

                if (resourceExists)
                {
                    // Update existing resource using appropriate method based on kind
                    switch (kind?.ToLowerInvariant() ?? string.Empty)
                    {
                        case "deployment":
                            var deploymentPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.AppsV1.PatchNamespacedDeploymentAsync(
                                body: deploymentPatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        case "service":
                            var servicePatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.CoreV1.PatchNamespacedServiceAsync(
                                body: servicePatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        case "ingress":
                            var ingressPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.NetworkingV1.PatchNamespacedIngressAsync(
                                body: ingressPatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        case "configmap":
                            var configMapPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.CoreV1.PatchNamespacedConfigMapAsync(
                                body: configMapPatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        case "secret":
                            var secretPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.CoreV1.PatchNamespacedSecretAsync(
                                body: secretPatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        case "statefulset":
                            var statefulSetPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.AppsV1.PatchNamespacedStatefulSetAsync(
                                body: statefulSetPatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        case "job":
                            var jobPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.BatchV1.PatchNamespacedJobAsync(
                                body: jobPatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        case "cronjob":
                            var cronJobPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.BatchV1.PatchNamespacedCronJobAsync(
                                body: cronJobPatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        case "daemonset":
                            var daemonSetPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.AppsV1.PatchNamespacedDaemonSetAsync(
                                body: daemonSetPatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        case "replicaset":
                            var replicaSetPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.AppsV1.PatchNamespacedReplicaSetAsync(
                                body: replicaSetPatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        case "pod":
                            var podPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.CoreV1.PatchNamespacedPodAsync(
                                body: podPatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        case "persistentvolumeclaim":
                            var pvcPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.CoreV1.PatchNamespacedPersistentVolumeClaimAsync(
                                body: pvcPatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        case "persistentvolume":
                            var pvPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.CoreV1.PatchPersistentVolumeAsync(
                                body: pvPatch,
                                name: resourceName);
                            break;

                        case "serviceaccount":
                            var serviceAccountPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.CoreV1.PatchNamespacedServiceAccountAsync(
                                body: serviceAccountPatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        case "role":
                            var rolePatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.RbacAuthorizationV1.PatchNamespacedRoleAsync(
                                body: rolePatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        case "rolebinding":
                            var roleBindingPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.RbacAuthorizationV1.PatchNamespacedRoleBindingAsync(
                                body: roleBindingPatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        case "clusterrole":
                            var clusterRolePatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.RbacAuthorizationV1.PatchClusterRoleAsync(
                                body: clusterRolePatch,
                                name: resourceName);
                            break;

                        case "clusterrolebinding":
                            var clusterRoleBindingPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.RbacAuthorizationV1.PatchClusterRoleBindingAsync(
                                body: clusterRoleBindingPatch,
                                name: resourceName);
                            break;

                        case "namespace":
                            var namespacePatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.CoreV1.PatchNamespaceAsync(
                                body: namespacePatch,
                                name: resourceName);
                            break;

                        case "networkpolicy":
                            var networkPolicyPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.NetworkingV1.PatchNamespacedNetworkPolicyAsync(
                                body: networkPolicyPatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        case "limitrange":
                            var limitRangePatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.CoreV1.PatchNamespacedLimitRangeAsync(
                                body: limitRangePatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        case "resourcequota":
                            var resourceQuotaPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.CoreV1.PatchNamespacedResourceQuotaAsync(
                                body: resourceQuotaPatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        default:
                            // Use generic method for other resource types
                            var genericPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            if (await IsClusterScopedResourceAsync(client, kind, GetApiGroup(apiVersion ?? string.Empty)))
                            {
                                await client.CustomObjects.PatchClusterCustomObjectAsync(
                                    body: genericPatch,
                                    group: GetApiGroup(apiVersion ?? string.Empty),
                                    version: GetApiVersion(apiVersion ?? string.Empty),
                                    plural: GetPluralFormForKind(kind ?? string.Empty),
                                    name: resourceName);
                            }
                            else
                            {
                                await client.CustomObjects.PatchNamespacedCustomObjectAsync(
                                    body: genericPatch,
                                    group: GetApiGroup(apiVersion ?? string.Empty),
                                    version: GetApiVersion(apiVersion ?? string.Empty),
                                    namespaceParameter: namespaceName,
                                    plural: GetPluralFormForKind(kind ?? string.Empty),
                                    name: resourceName);
                            }
                            break;
                    }

                    var group = Constants.KubernetesCoreGroup;
                    _crawlerTriggerService.TriggerKubernetesCrawl(
                        resourceId,
                        namespaceName,
                        resourceName,
                        k8sObj.ApiVersion.Contains("/") ? k8sObj.ApiVersion.Split('/')[0] : group,
                        k8sObj.ApiVersion.Contains("/") ? k8sObj.ApiVersion.Split('/')[1] : k8sObj.ApiVersion,
                        k8sObj.Kind);

                    return $"Successfully updated {kind}/{resourceName}" +
                           (string.IsNullOrEmpty(namespaceName) ? "" : $" in namespace '{namespaceName}'");
                }
                else
                {
                    switch (kind?.ToLowerInvariant() ?? string.Empty)
                    {
                        case "deployment":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var deployment = JsonConvert.DeserializeObject<V1Deployment>(jsonBody);
                            await client.AppsV1.CreateNamespacedDeploymentAsync(
                                body: deployment,
                                namespaceParameter: namespaceName);
                            break;

                        case "service":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var service = JsonConvert.DeserializeObject<V1Service>(jsonBody);
                            await client.CoreV1.CreateNamespacedServiceAsync(
                                body: service,
                                namespaceParameter: namespaceName);
                            break;

                        case "ingress":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var ingress = JsonConvert.DeserializeObject<V1Ingress>(jsonBody);
                            await client.NetworkingV1.CreateNamespacedIngressAsync(
                                body: ingress,
                                namespaceParameter: namespaceName);
                            break;

                        case "configmap":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var configMap = JsonConvert.DeserializeObject<V1ConfigMap>(jsonBody);
                            await client.CoreV1.CreateNamespacedConfigMapAsync(
                                body: configMap,
                                namespaceParameter: namespaceName);
                            break;

                        case "secret":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var secret = JsonConvert.DeserializeObject<V1Secret>(jsonBody);
                            await client.CoreV1.CreateNamespacedSecretAsync(
                                body: secret,
                                namespaceParameter: namespaceName);
                            break;

                        case "statefulset":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var statefulSet = JsonConvert.DeserializeObject<V1StatefulSet>(jsonBody);
                            await client.AppsV1.CreateNamespacedStatefulSetAsync(
                                body: statefulSet,
                                namespaceParameter: namespaceName);
                            break;

                        case "job":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var job = JsonConvert.DeserializeObject<V1Job>(jsonBody);
                            await client.BatchV1.CreateNamespacedJobAsync(
                                body: job,
                                namespaceParameter: namespaceName);
                            break;

                        case "cronjob":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var cronJob = JsonConvert.DeserializeObject<V1CronJob>(jsonBody);
                            await client.BatchV1.CreateNamespacedCronJobAsync(
                                body: cronJob,
                                namespaceParameter: namespaceName);
                            break;

                        case "daemonset":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var daemonSet = JsonConvert.DeserializeObject<V1DaemonSet>(jsonBody);
                            await client.AppsV1.CreateNamespacedDaemonSetAsync(
                                body: daemonSet,
                                namespaceParameter: namespaceName);
                            break;

                        case "replicaset":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var replicaSet = JsonConvert.DeserializeObject<V1ReplicaSet>(jsonBody);
                            await client.AppsV1.CreateNamespacedReplicaSetAsync(
                                body: replicaSet,
                                namespaceParameter: namespaceName);
                            break;

                        case "pod":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var pod = JsonConvert.DeserializeObject<V1Pod>(jsonBody);
                            await client.CoreV1.CreateNamespacedPodAsync(
                                body: pod,
                                namespaceParameter: namespaceName);
                            break;

                        case "persistentvolumeclaim":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var pvc = JsonConvert.DeserializeObject<V1PersistentVolumeClaim>(jsonBody);
                            await client.CoreV1.CreateNamespacedPersistentVolumeClaimAsync(
                                body: pvc,
                                namespaceParameter: namespaceName);
                            break;

                        case "persistentvolume":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName}");
                            var pv = JsonConvert.DeserializeObject<V1PersistentVolume>(jsonBody);
                            await client.CoreV1.CreatePersistentVolumeAsync(
                                body: pv);
                            break;

                        case "serviceaccount":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var serviceAccount = JsonConvert.DeserializeObject<V1ServiceAccount>(jsonBody);
                            await client.CoreV1.CreateNamespacedServiceAccountAsync(
                                body: serviceAccount,
                                namespaceParameter: namespaceName);
                            break;

                        case "role":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var role = JsonConvert.DeserializeObject<V1Role>(jsonBody);
                            await client.RbacAuthorizationV1.CreateNamespacedRoleAsync(
                                body: role,
                                namespaceParameter: namespaceName);
                            break;

                        case "rolebinding":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var roleBinding = JsonConvert.DeserializeObject<V1RoleBinding>(jsonBody);
                            await client.RbacAuthorizationV1.CreateNamespacedRoleBindingAsync(
                                body: roleBinding,
                                namespaceParameter: namespaceName);
                            break;

                        case "clusterrole":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName}");
                            var clusterRole = JsonConvert.DeserializeObject<V1ClusterRole>(jsonBody);
                            await client.RbacAuthorizationV1.CreateClusterRoleAsync(
                                body: clusterRole);
                            break;

                        case "clusterrolebinding":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName}");
                            var clusterRoleBinding = JsonConvert.DeserializeObject<V1ClusterRoleBinding>(jsonBody);
                            await client.RbacAuthorizationV1.CreateClusterRoleBindingAsync(
                                body: clusterRoleBinding);
                            break;

                        case "namespace":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName}");
                            var namespaceObj = JsonConvert.DeserializeObject<V1Namespace>(jsonBody);
                            await client.CoreV1.CreateNamespaceAsync(
                                body: namespaceObj);
                            break;

                        case "networkpolicy":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var networkPolicy = JsonConvert.DeserializeObject<V1NetworkPolicy>(jsonBody);
                            await client.NetworkingV1.CreateNamespacedNetworkPolicyAsync(
                                body: networkPolicy,
                                namespaceParameter: namespaceName);
                            break;

                        case "limitrange":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var limitRange = JsonConvert.DeserializeObject<V1LimitRange>(jsonBody);
                            await client.CoreV1.CreateNamespacedLimitRangeAsync(
                                body: limitRange,
                                namespaceParameter: namespaceName);
                            break;

                        case "resourcequota":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");
                            var resourceQuota = JsonConvert.DeserializeObject<V1ResourceQuota>(jsonBody);
                            await client.CoreV1.CreateNamespacedResourceQuotaAsync(
                                body: resourceQuota,
                                namespaceParameter: namespaceName);
                            break;

                        default:
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName}" +
                                (await IsClusterScopedResourceAsync(client, kind, GetApiGroup(apiVersion ?? string.Empty)) ? "" : $" in namespace {namespaceName}"));
                            // Use generic method for other resource types
                            if (await IsClusterScopedResourceAsync(client, kind, GetApiGroup(apiVersion ?? string.Empty)))
                            {
                                await client.CustomObjects.CreateClusterCustomObjectAsync(
                                    body: yamlObject,
                                    group: GetApiGroup(apiVersion ?? string.Empty),
                                    version: GetApiVersion(apiVersion ?? string.Empty),
                                    plural: GetPluralFormForKind(kind ?? string.Empty));
                            }
                            else
                            {
                                await client.CustomObjects.CreateNamespacedCustomObjectAsync(
                                    body: yamlObject,
                                    group: GetApiGroup(apiVersion ?? string.Empty),
                                    version: GetApiVersion(apiVersion ?? string.Empty),
                                    namespaceParameter: namespaceName,
                                    plural: GetPluralFormForKind(kind ?? string.Empty));
                            }
                            break;
                    }

                    var group = Constants.KubernetesCoreGroup;
                    _crawlerTriggerService.TriggerKubernetesCrawl(
                        resourceId,
                        namespaceName,
                        resourceName,
                        k8sObj.ApiVersion.Contains("/") ? k8sObj.ApiVersion.Split('/')[0] : group,
                        k8sObj.ApiVersion.Contains("/") ? k8sObj.ApiVersion.Split('/')[1] : k8sObj.ApiVersion,
                        k8sObj.Kind);

                    return $"Successfully created {kind}/{resourceName}" +
                           (string.IsNullOrEmpty(namespaceName) ? "" : $" in namespace '{namespaceName}'");
                }
            }
            catch (k8s.Autorest.HttpOperationException ex) when (ex.Response?.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger?.LogInternalError(ex, "Forbidden error applying Kubernetes YAML to cluster {ResourceId}", resourceId);
                return await GetPermissionErrorMessageAsync(resourceId);
            }
            catch (k8s.Autorest.HttpOperationException ex) when (ex.Response?.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger?.LogInternalError(ex, "Unauthorized error applying Kubernetes YAML to cluster {ResourceId}", resourceId);
                return "Failed to run kubectl command. Error from AKS API Server: Unauthorized.\n" +
                    $"Authentication failed. Please ensure valid credentials are provided for {_agentKubeCtlIdentity}.";
            }
            catch (k8s.Autorest.HttpOperationException ex) when (ex.Response?.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _logger?.LogInternalError(ex, "Conflict error applying Kubernetes YAML to cluster {ResourceId}", resourceId);
                return $"Failed to apply Kubernetes YAML due to conflict. The resource may have been modified by another process. Details: {ex.Message}";
            }
            catch (k8s.Autorest.HttpOperationException ex)
            {
                _logger?.LogInternalError(ex, "HTTP operation error applying Kubernetes YAML to cluster {ResourceId}. Status: {StatusCode}", resourceId, ex.Response?.StatusCode);
                return $"Failed to apply Kubernetes YAML. HTTP {ex.Response?.StatusCode}: {ex.Message}";
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Unexpected error applying Kubernetes YAML to cluster {ResourceId}", resourceId);
                return $"Error applying YAML: {ex.Message}";
            }
        }

        /// <summary>
        /// Extracts the API group from the apiVersion string
        /// </summary>
        private static string GetApiGroup(string apiVersion)
        {
            if (string.IsNullOrEmpty(apiVersion) || !apiVersion.Contains('/'))
            {
                return ""; // Core API group
            }

            return apiVersion.Split('/')[0];
        }

        /// <summary>
        /// Extracts the API version from the apiVersion string
        /// </summary>
        private static string GetApiVersion(string apiVersion)
        {
            if (string.IsNullOrEmpty(apiVersion))
            {
                return "v1"; // Default version
            }

            if (!apiVersion.Contains('/'))
            {
                return apiVersion; // Just the version
            }

            return apiVersion.Split('/')[1];
        }

        /// <summary>
        /// Returns the plural form for common Kubernetes resource kinds
        /// </summary>
        private static string GetPluralFormForKind(string kind)
        {
            return kind.ToLowerInvariant() switch
            {
                "deployment" => "deployments",
                "statefulset" => "statefulsets",
                "service" => "services",
                "ingress" => "ingresses",
                "configmap" => "configmaps",
                "secret" => "secrets",
                "pod" => "pods",
                "job" => "jobs",
                "cronjob" => "cronjobs",
                "persistentvolumeclaim" => "persistentvolumeclaims",
                "persistentvolume" => "persistentvolumes",
                "daemonset" => "daemonsets",
                "replicaset" => "replicasets",
                "role" => "roles",
                "rolebinding" => "rolebindings",
                "clusterrole" => "clusterroles",
                "clusterrolebinding" => "clusterrolebindings",
                "serviceaccount" => "serviceaccounts",
                "namespace" => "namespaces",
                "networkpolicy" => "networkpolicies",
                "limitrange" => "limitranges",
                "resourcequota" => "resourcequotas",
                _ => kind.ToLowerInvariant() + "s" // Simple pluralization for unknown kinds
            };
        }

        /// <summary>
        /// Determines if a Kubernetes resource kind is cluster-scoped
        /// For CRDs, fetches the CustomResourceDefinition to check the scope
        /// </summary>
        private async Task<bool> IsClusterScopedResourceAsync(IKubernetes client, string? kind, string? apiGroup)
        {
            // Handle built-in Kubernetes resources
            var builtInResult = kind?.ToLowerInvariant() switch
            {
                // Built-in cluster-scoped resources
                "clusterrole" => true,
                "clusterrolebinding" => true,
                "namespace" => true,
                "persistentvolume" => true,
                "node" => true,
                "storageclass" => true,
                "customresourcedefinition" => true,

                // Built-in namespace-scoped resources
                "deployment" => false,
                "service" => false,
                "ingress" => false,
                "configmap" => false,
                "secret" => false,
                "statefulset" => false,
                "job" => false,
                "cronjob" => false,
                "daemonset" => false,
                "replicaset" => false,
                "pod" => false,
                "persistentvolumeclaim" => false,
                "serviceaccount" => false,
                "role" => false,
                "rolebinding" => false,
                "networkpolicy" => false,
                "limitrange" => false,
                "resourcequota" => false,
                "endpoints" => false,
                "event" => false,
                "replicationcontroller" => false,
                "horizontalpodautoscaler" => false,
                "verticalpodautoscaler" => false,
                "poddisruptionbudget" => false,
                "lease" => false,

                _ => (bool?)null
            };

            if (builtInResult.HasValue)
            {
                return builtInResult.Value;
            }

            // For custom resources, check the CRD definition
            if (!string.IsNullOrEmpty(apiGroup) && !string.IsNullOrEmpty(kind))
            {
                try
                {
                    var crds = await client.ApiextensionsV1.ListCustomResourceDefinitionAsync();
                    var crd = crds.Items.FirstOrDefault(c => c.Spec.Group == apiGroup &&
                        c.Spec.Names.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase));

                    if (crd != null)
                    {
                        // Check the scope field in the CRD spec
                        return crd.Spec.Scope.Equals("Cluster", StringComparison.OrdinalIgnoreCase);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogInternalWarning("Failed to fetch CRD definition for kind {Kind} in group {ApiGroup}: {Message}", kind, apiGroup, ex.Message);
                }
            }

            // Default to namespaced for unknown resources
            return false;
        }

        /// <summary>
        /// Custom JSON converter to preserve string values that might be interpreted as numbers
        /// </summary>
        private class StringPreservingConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                // Only process string values
                return objectType == typeof(string);
            }

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                // Return the raw value as a string
                return reader.Value?.ToString();
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                // Always write strings as strings, even if they look like numbers
                writer.WriteValue(value?.ToString() ?? string.Empty);
            }
        }

        /// <summary>
        /// Represents Kubernetes metadata including name and namespace
        /// </summary>
        private class k8sMetadata
        {
            public string? Name { get; set; }
            public string? Namespace { get; set; }
        }

        /// <summary>
        /// Represents a Kubernetes object with apiVersion, kind and metadata
        /// </summary>
        private class k8sObject
        {
            public string? ApiVersion { get; set; }
            public string? Kind { get; set; }
            public k8sMetadata? Metadata { get; set; }
            public Dictionary<string, object?>? Spec { get; set; }
            public Dictionary<string, object?>? Status { get; set; }
        }
    }
}
