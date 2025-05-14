// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Logging;
using k8s;
using k8s.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;
using Newtonsoft.Json.Converters;
using Agent.Logging;

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
                var client = await GetOrCreateClientAsync(resourceId);

                // Parse YAML with a single deserializer that preserves numeric types
                var yamlDeserializer = new DeserializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                    .Build();

                // Parse the provided YAML into a dynamic object (only once)
                var yaml = new StringReader(yamlContent);
                var yamlObject = yamlDeserializer.Deserialize(yaml);

                // Convert to dictionary for metadata extraction
                var jsonSettings = new JsonSerializerSettings
                {
                    Converters = new List<JsonConverter> { new StringPreservingConverter() }
                };
                var jsonString = JsonConvert.SerializeObject(yamlObject, jsonSettings);
                var tempObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString, jsonSettings);

                if (tempObj == null || !tempObj.ContainsKey("kind") || !tempObj.ContainsKey("apiVersion"))
                {
                    return "Error: Invalid YAML document: Missing 'kind' or 'apiVersion'";
                }

                var kind = tempObj["kind"].ToString();
                var apiVersion = tempObj["apiVersion"].ToString();
                string namespaceName = "default";
                string resourceName = "";

                _logger?.LogInternalInformation("Parsing YAML object with kind: {Kind}, apiVersion: {ApiVersion}", kind, apiVersion);

                // Extract namespace and name from metadata if present
                if (tempObj.TryGetValue("metadata", out var metadataObj))
                {
                    _logger?.LogDebug("Found metadata object of type: {MetadataType}", metadataObj?.GetType().FullName ?? "null");

                    // Handle different possible types for metadata
                    IDictionary<string, object> metadata;
                    if (metadataObj is IDictionary<string, object> metadataDict)
                    {
                        metadata = metadataDict;
                    }
                    else if (metadataObj is JObject jObj)
                    {
                        // Handle JObject type specifically
                        metadata = jObj.ToObject<Dictionary<string, object>>();
                        _logger?.LogDebug("Converted JObject metadata to Dictionary");
                    }
                    else
                    {
                        _logger?.LogInternalWarning("Metadata is not in expected format: {MetadataType}", metadataObj?.GetType().FullName ?? "null");
                        metadata = new Dictionary<string, object>();
                    }

                    // Extract namespace
                    if (metadata.TryGetValue("namespace", out var namespaceObj) && namespaceObj != null)
                    {
                        namespaceName = namespaceObj.ToString();
                        _logger?.LogDebug("Found namespace: {Namespace}", namespaceName);
                    }

                    // Extract name
                    if (metadata.TryGetValue("name", out var nameObj) && nameObj != null)
                    {
                        resourceName = nameObj.ToString();
                        _logger?.LogDebug("Found resource name: {ResourceName}", resourceName);
                    }
                }
                else
                {
                    _logger?.LogInternalWarning("No 'metadata' field found in YAML");
                }

                _logger?.LogInternalInformation($"Applying new resource {kind}/{resourceName} in namespace {namespaceName}");


                // Convert the already parsed YAML object to JSON with proper numeric handling
                var jsonSerializer = new Newtonsoft.Json.JsonSerializer();
                jsonSerializer.Converters.Add(new StringEnumConverter());
                jsonSerializer.Converters.Add(new StringPreservingConverter());
                jsonSerializer.NullValueHandling = NullValueHandling.Ignore;

                var stringWriter = new StringWriter();
                using (var jsonWriter = new JsonTextWriter(stringWriter))
                {
                    jsonSerializer.Serialize(jsonWriter, yamlObject);
                }

                var jsonBody = stringWriter.ToString();
                _logger?.LogDebug("Converted YAML to JSON: {JsonBody}", jsonBody);

                // Create patch and apply it - CHANGE THIS PART
                bool resourceExists = false;

                try
                {
                    // Check if resource exists
                    var existingResource = await client.CustomObjects.GetNamespacedCustomObjectAsync(
                        group: GetApiGroup(apiVersion),
                        version: GetApiVersion(apiVersion),
                        namespaceParameter: namespaceName,
                        plural: GetPluralFormForKind(kind),
                        name: resourceName);

                    resourceExists = (existingResource != null);
                    _logger?.LogInternalInformation("Resource {Kind}/{Name} already exists, will update", kind, resourceName);
                }
                catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger?.LogInternalInformation("Resource {Kind}/{Name} does not exist, will create", kind, resourceName);
                    resourceExists = false;
                }

                if (resourceExists)
                {
                    // Update existing resource using appropriate method based on kind
                    switch (kind.ToLowerInvariant())
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

                        case "statefulset":
                            var statefulSetPatch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.AppsV1.PatchNamespacedStatefulSetAsync(
                                body: statefulSetPatch,
                                name: resourceName,
                                namespaceParameter: namespaceName);
                            break;

                        default:
                            // Use generic method for other resource types
                            var patch = new V1Patch(jsonBody, V1Patch.PatchType.StrategicMergePatch);
                            await client.CustomObjects.PatchNamespacedCustomObjectAsync(
                                body: patch,
                                group: GetApiGroup(apiVersion),
                                version: GetApiVersion(apiVersion),
                                namespaceParameter: namespaceName,
                                plural: GetPluralFormForKind(kind),
                                name: resourceName);
                            break;
                    }

                    return $"Successfully updated {kind}/{resourceName} in namespace '{namespaceName}'";
                }
                else
                {
                    // Create new resource using appropriate method based on kind
                    switch (kind.ToLowerInvariant())
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

                        case "statefulset":
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");

                            var statefulSet = JsonConvert.DeserializeObject<V1StatefulSet>(jsonBody);
                            await client.AppsV1.CreateNamespacedStatefulSetAsync(
                                body: statefulSet,
                                namespaceParameter: namespaceName);
                            break;

                        default:
                            _logger?.LogInternalInformation($"Creating new resource {kind}/{resourceName} in namespace {namespaceName}");

                            // Use generic method for other resource types
                            await client.CustomObjects.CreateNamespacedCustomObjectAsync(
                                body: yamlObject,
                                group: GetApiGroup(apiVersion),
                                version: GetApiVersion(apiVersion),
                                namespaceParameter: namespaceName,
                                plural: GetPluralFormForKind(kind));
                            break;
                    }

                    return $"Successfully created {kind}/{resourceName} in namespace '{namespaceName}'";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Error applying Kubernetes YAML to cluster {ResourceId}", resourceId);
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
                _ => kind.ToLowerInvariant() + "s" // Simple pluralization for unknown kinds
            };
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

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                // Return the raw value as a string
                return reader.Value?.ToString();
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                // Always write strings as strings, even if they look like numbers
                writer.WriteValue(value.ToString());
            }
        }
    }
}
