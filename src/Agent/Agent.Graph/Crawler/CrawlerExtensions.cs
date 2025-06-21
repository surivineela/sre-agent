// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
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
        return client.Query($"g.V('{GetSanitizedCosmosDBId(node.GetNodeId())}').outE().not(__.has('nonCrawled', 'True')).or(__.not(has('updateTs')),__.has('updateTs', P.lt({ts}))).drop()");
    }

    public static Task SoftDeleteStaleNodesWithFilter(IGraphDatabaseClient client, IDictionary<string, string> props, DateTimeOffset deleteBefore)
    {
        var ts = deleteBefore.AddMinutes(-35).Ticks; // offset by 35 mins since the crawler runs every 30 mins.
        var filter = string.Join(",", props.Select(kvp => $"has('{kvp.Key}','{kvp.Value}')"));
        return client.Query($"g.V().and({filter}).not(__.has('nonCrawled', 'True')).has('updateTs', P.lt({ts})).property('isDeleted', true).property('updateTs', {ts})");
    }

    public static string GetSanitizedCosmosDBId(string id)
    {
        return id.ToLowerInvariant().Replace("/", "_").Replace(":", "_").Replace(" ", "_");
    }
}

