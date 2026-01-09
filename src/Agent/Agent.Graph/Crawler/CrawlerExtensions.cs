// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Graph.Crawler.ARM;

namespace Agent.Graph.Crawler;

public static class CrawlerExtensions
{
    public static ArmResourceEdge AddNetworkIngressEdgeProperties(this ArmResourceEdge edge)
    {
        edge.AdditionalProperties.AddOrUpdateEdgeProperty(Constants.NetworkPathKey, Constants.NetworkPathIngress);
        return edge;
    }

    public static ArmResourceEdge AddNetworkEgressEdgeProperties(this ArmResourceEdge edge)
    {
        edge.AdditionalProperties.AddOrUpdateEdgeProperty(Constants.NetworkPathKey, Constants.NetworkPathEgress);
        return edge;
    }

    public static ArmResourceEdge AddRbacInheritedEdgeProperties(this ArmResourceEdge edge)
    {
        edge.AdditionalProperties.AddOrUpdateEdgeProperty(Constants.RbacPath, Constants.RbacPathInherited);
        return edge;
    }

    public static ArmResourceEdge AddRbacExplicitEdgeProperties(this ArmResourceEdge edge)
    {
        edge.AdditionalProperties.AddOrUpdateEdgeProperty(Constants.RbacPath, Constants.RbacPathExplicit);
        return edge;
    }

    public static ArmResourceEdge AddOrUpdateEdgeProperty(this ArmResourceEdge edge, string key, string val)
    {
        edge.AdditionalProperties.AddOrUpdateEdgeProperty(key, val);
        return edge;
    }

    public static ArmResourceEdge AddReferenceVolumeMountProperties(this ArmResourceEdge edge)
    {
        edge.AdditionalProperties.AddOrUpdateEdgeProperty(Constants.ReferenceTypeKey, Constants.ReferenceTypeVolumeMount);
        return edge;
    }

    public static ArmResourceEdge AddReferencePersistentVolumeClaimProperties(this ArmResourceEdge edge)
    {
        edge.AdditionalProperties.AddOrUpdateEdgeProperty(Constants.ReferenceTypeKey, Constants.ReferenceTypePersistentVolumeClaim);
        return edge;
    }

    public static ArmResourceEdge AddReferenceEnvProperties(this ArmResourceEdge edge)
    {
        edge.AdditionalProperties.AddOrUpdateEdgeProperty(Constants.ReferenceTypeKey, Constants.ReferenceTypeEnv);
        return edge;
    }

    public static ArmResourceEdge AddBackendStatusReadyProperties(this ArmResourceEdge edge)
    {
        edge.AdditionalProperties.AddOrUpdateEdgeProperty(Constants.BackendStatusKey, Constants.BackendStatusReady);
        return edge;
    }

    public static ArmResourceEdge AddBackendStatusNotReadyProperties(this ArmResourceEdge edge)
    {
        edge.AdditionalProperties.AddOrUpdateEdgeProperty(Constants.BackendStatusKey, "NotReady");
        return edge;
    }

    public static IDictionary<string, object> AddOrUpdateEdgeProperty(this IDictionary<string, object> props, string key, string val)
    {
        if (props.ContainsKey(key))
        {
            props[key] = val;
        }
        else
        {
            props.Add(key, val);
        }

        return props;
    }

    public static Task RemoveStaleEdgeForNode(IGraphDatabaseClient client, GraphNode node, long ts)
    {
        return client.Query($"g.V('{GetSanitizedCosmosDBId(node.GetNodeId())}').outE().not(__.has('nonCrawled', true)).or(__.not(has('updateTs')),__.has('updateTs', P.lt({ts}))).drop()");
    }

    public static Task SoftDeleteStaleNodesWithFilter(IGraphDatabaseClient client, IReadOnlyDictionary<string, string> props, DateTimeOffset deleteBeforeTimestamp, bool azureResourcesOnly = false)
    {
        var updateTs = DateTimeOffset.UtcNow.Ticks;
        var deleteBeforeWithOffset = deleteBeforeTimestamp.AddMinutes(-35).Ticks; // offset by 35 mins since the crawler runs every 30 mins.

        var queryBuilder = new StringBuilder("g.V()");

        // Add scope filter if properties provided
        if (props.Count > 0)
        {
            var filter = string.Join(",", props.Select(kvp => $"has('{kvp.Key}','{kvp.Value}')"));
            queryBuilder.Append($".and({filter})");
        }

        // Only clean up Azure resource nodes (ARM resources have subscriptionId, K8s resources have clusterResourceId)
        if (azureResourcesOnly)
        {
            queryBuilder.Append(".or(__.has('subscriptionId'), __.has('clusterResourceId'))");
        }

        // Common query parts - use not(has('isDeleted', true)) to also match nodes without the property
        queryBuilder.Append(".not(__.has('isDeleted', true))");
        queryBuilder.Append(".not(__.has('nonCrawled', true))");
        queryBuilder.Append($".has('updateTs', P.lt({deleteBeforeWithOffset}))");
        queryBuilder.Append(".property('isDeleted', true)");
        queryBuilder.Append($".property('updateTs', {updateTs})");

        return client.Query(queryBuilder.ToString());
    }

    public static string GetSanitizedCosmosDBId(string id)
    {
        var normalizedId = StripQueryAndFragment(id);
        return normalizedId.ToLowerInvariant()
            .Replace("/", "_")
            .Replace("\\", "_")
            .Replace(":", "_")
            .Replace(" ", "_")
            .Replace("?", "_")
            .Replace("#", "_");
    }

    private static string StripQueryAndFragment(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return id;
        }

        var index = id.IndexOfAny(['?', '#']);
        return index >= 0 ? id[..index] : id;
    }
}

