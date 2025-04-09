// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.Attributes;
using Azure.Core;
using k8s;

namespace Agent.Data.DatabaseClients.GraphDbClient;
public class KubernetesResourceNode : GraphNode
{
    // Set to the k8s resource object to avoid fetching it twice
    public IKubernetesObject ResourceObject { get; set; }
    // the cluster arm resource id
    [GraphProperty("clusterResourceId")]
    public string ClusterResourceId { get; set; }

    [GraphProperty("name")]
    public string Name { get; set; }

    [GraphProperty("group")]
    public string Group { get; set; }

    [GraphProperty("apiVersion")]
    public string ApiVersion { get; set; }

    [GraphProperty("kind")]
    public string Kind { get; set; }
    public IDictionary<string, string> Annotations { get; set; }
    public IDictionary<string, string> Labels { get; set; }

    public KubernetesResourceNode(
        IKubernetesObject k8sObject,
        string clusterResourceId,
        string name,
        string group,
        string apiVersion,
        string kind,
        IDictionary<string, string> annotations = null,
        IDictionary<string, string> labels = null)
    {
        UpdateTs = DateTime.UtcNow.Ticks;
        ResourceObject = k8sObject;
        ClusterResourceId = clusterResourceId.ToLowerInvariant();
        Name = name.ToLowerInvariant();
        Group = group.ToLowerInvariant();
        ApiVersion = apiVersion.ToLowerInvariant();
        Kind = kind.ToLowerInvariant();
        Annotations = annotations;
        Labels = labels;
    }

    public override string GetNodeLabel()
    {
        // Return a standardized label combining resource type and kind
        return $"k8s/{Group}/{ApiVersion}/{Kind}";
    }

    public override string GetNodeId()
    {
        // This should already be in the format: {clusterResourceId}/{group}/{version}/{kind}/{name}
        return $"{ClusterResourceId}/{Group}/{ApiVersion}/{Kind}/{Name}";
        ;
    }

    public override string GetResourceType()
    {
        // Return the Kubernetes resource type in a standardized format
        return $"k8s/{Group}/{ApiVersion}/{Kind}";
    }

    public override IDictionary<string, object> GetNodeProperties()
    {
        var props = base.GetNodeProperties();

        //TODO: can property value be a dictionary?
        if (Annotations != null)
        {
            foreach (var annotation in Annotations)
            {
                if (string.Equals(annotation.Key, "kubectl.kubernetes.io/last-applied-configuration"))
                {
                    continue;
                }
                props.Add($"annotation_{annotation.Key}", annotation.Value);
            }
        }

        if (Labels != null)
        {
            foreach (var label in Labels)
            {
                props.Add($"label_{label.Key}", label.Value);
            }
        }

        return props;
    }

    public override string GetHashString()
    {
        return $"{GetNodeId()}";
    }

    public override string GetSubscriptionId()
    {
        // Extract the subscription ID from the cluster resource ID
        var id = new ResourceIdentifier(ClusterResourceId);
        if (id == null)
        {
            return string.Empty;
        }

        return id.SubscriptionId;
    }
}

