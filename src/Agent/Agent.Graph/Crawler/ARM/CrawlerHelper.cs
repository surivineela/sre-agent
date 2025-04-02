using Agent.Data.DatabaseClients.GraphDbClient;

namespace Agent.Graph.Crawler.ARM;

public static class CrawlerHelper
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
}
