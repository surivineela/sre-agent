// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.Attributes;
using k8s;

namespace Agent.Data.DatabaseClients.GraphDbClient;

public class KubernetesNamespacedResourceNode : KubernetesResourceNode
{
    [GraphProperty("namespace")]
    public string Namespace { get; set; }

    public KubernetesNamespacedResourceNode(
        IKubernetesObject? k8sObject,
        string clusterResourceId,
        string @namespace,
        string? subscriptionId,
        string? resourceGroupName,
        string? location,
        string resourceName,
        string group,
        string apiVersion,
        string kind,
        IDictionary<string, string>? annotations = null,
        IDictionary<string, string>? labels = null) : base(k8sObject, clusterResourceId, subscriptionId, resourceGroupName, location, resourceName, group, apiVersion, kind, annotations, labels)
    {
        UpdateTs = DateTime.UtcNow.Ticks;
        Namespace = @namespace.ToLowerInvariant();
    }

    public override string GetNodeId()
    {
        // This should already be in the format: {clusterResourceId}/{group}/{version}/namespaces/{namespace}/{kind}/{resourceName}
        return $"{ClusterResourceId}/{Group}/{ApiVersion}/namespaces/{Namespace}/{Kind}/{ResourceName}";
        ;
    }
}

