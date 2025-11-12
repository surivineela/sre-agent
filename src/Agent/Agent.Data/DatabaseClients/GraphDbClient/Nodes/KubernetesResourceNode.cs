// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.Attributes;
using Azure.Core;
using k8s;

namespace Agent.Data.DatabaseClients.GraphDbClient;

public class KubernetesResourceNode : GraphNode
{
    // Set to the k8s resource object to avoid fetching it twice
    public IKubernetesObject? ResourceObject { get; set; }

    [GraphProperty("subscriptionId")]
    public string? SubscriptionId { get; set; }

    [GraphProperty("resourceGroupName")]
    public string? ResourceGroupName { get; set; }

    [GraphProperty("location")]
    public string? Location { get; set; }

    // the cluster arm resource id
    [GraphProperty("clusterResourceId")]
    public string ClusterResourceId { get; set; }

    [GraphProperty("resourceName")]
    public string ResourceName { get; set; }

    [GraphProperty("group")]
    public string Group { get; set; }

    [GraphProperty("apiVersion")]
    public string ApiVersion { get; set; }

    [GraphProperty("kind")]
    public string Kind { get; set; }
    public IDictionary<string, string>? Annotations { get; set; }
    public IDictionary<string, string>? Labels { get; set; }

    [GraphJsonProperty("appHealthInfo")]
    public AppHealthInfo? AppHealthInfo { get; set; }

    public KubernetesResourceNode(
        IKubernetesObject? k8sObject,
        string clusterResourceId,
        string? subscriptionId,
        string? resourceGroupName,
        string? location,
        string resourceName,
        string group,
        string apiVersion,
        string kind,
        IDictionary<string, string>? annotations = null,
        IDictionary<string, string>? labels = null)
    {
        SubscriptionId = subscriptionId?.ToLowerInvariant() ?? string.Empty;
        ResourceGroupName = resourceGroupName?.ToLowerInvariant() ?? string.Empty;
        Location = !string.IsNullOrEmpty(location) ? location.NormalizeLocation() : string.Empty;
        UpdateTs = DateTime.UtcNow.Ticks;
        ResourceObject = k8sObject;
        ClusterResourceId = clusterResourceId.ToLowerInvariant();
        ResourceName = resourceName.ToLowerInvariant();
        Group = group.ToLowerInvariant();
        ApiVersion = apiVersion.ToLowerInvariant();
        Kind = kind.ToLowerInvariant();
        Annotations = annotations;
        Labels = labels;

        if (string.IsNullOrEmpty(subscriptionId) && !string.IsNullOrEmpty(clusterResourceId))
        {
            var id = new ResourceIdentifier(clusterResourceId);
            SubscriptionId = id.SubscriptionId?.ToLowerInvariant();
            ResourceGroupName = id.ResourceGroupName?.ToLowerInvariant();
        }
    }

    public override string GetNodeLabel()
    {
        // Return a standardized label combining resource type and kind
        return $"k8s/{Group}/{ApiVersion}/{Kind}";
    }

    public override string GetNodeId()
    {
        // This should already be in the format: {clusterResourceId}/{group}/{version}/{kind}/{resourceName}
        return $"{ClusterResourceId}/{Group}/{ApiVersion}/{Kind}/{ResourceName}";
        ;
    }

    public override string GetResourceType()
    {
        // Return the Kubernetes resource type in a standardized format
        return $"k8s/{Group}/{ApiVersion}/{Kind}";
    }

    public override string GetResourceKind()
    {
        // Return the Kubernetes resource kind in a standardized format
        return ResourceKindHelper.getResourceKind(GetResourceType(), Kind);
    }

    public override void SetResourceKind(string? newResourceKind)
    {
        return;
    }

    public override IDictionary<string, object> GetNodeProperties()
    {
        var props = base.GetNodeProperties();

        // Remove location property if it's empty to avoid overwriting existing location in database
        if (string.IsNullOrEmpty(Location))
        {
            props.Remove("location");
        }

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

        return id.SubscriptionId ?? string.Empty;
    }
}

