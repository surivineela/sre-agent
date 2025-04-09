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
        string name,
        string group,
        string apiVersion,
        string kind,
        IDictionary<string, string> annotations = null,
        IDictionary<string, string> labels = null) : base(k8sObject, clusterResourceId, name, group, apiVersion, kind, annotations, labels)
    {
        Namespace = @namespace.ToLowerInvariant();
    }

    public override string GetNodeId()
    {
        // This should already be in the format: {clusterResourceId}/{group}/{version}/namespaces/{namespace}/{kind}/{name}
        return $"{ClusterResourceId}/{Group}/{ApiVersion}/namespaces/{Namespace}/{Kind}/{Name}";
        ;
    }
}

