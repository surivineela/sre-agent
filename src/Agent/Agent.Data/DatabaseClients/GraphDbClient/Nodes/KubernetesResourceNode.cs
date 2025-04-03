using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using k8s;

namespace Agent.Data.DatabaseClients.GraphDbClient;
public class KubernetesResourceNode : GraphNode
{
    // Set to the k8s resource object to avoid fetching it twice
    public IKubernetesObject ResourceObject { get; set; }
    // the cluster arm resource id
    public string ClusterResourceId { get; set; }
    public string Name { get; set; }
    public string Group { get; set; }
    public string ApiVersion { get; set; }
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
        // Return all relevant properties as a dictionary
        var properties = new Dictionary<string, object>
            {
                { "updateTs", UpdateTs},
                { "clusterResourceId", ClusterResourceId },
                { "name", Name},
                { "group", Group },
                { "apiVersion", ApiVersion },
                { "kind", Kind },
            };

        //TODO: can property value be a dictionary?
        if (Annotations != null)
        {
            foreach (var annotation in Annotations)
            {
                properties.Add($"annotation_{annotation.Key}", annotation.Value);
            }
        }

        if (Labels != null)
        {
            foreach (var label in Labels)
            {
                properties.Add($"label_{label.Key}", label.Value);
            }
        }

        return properties;
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
