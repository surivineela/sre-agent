// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient.Nodes;

public class SourceCodeRepoNode(string repoUrl) : GraphNode(new Dictionary<string, object>() {
            { "nonCrawled", true }
        })
{
    [GraphProperty("repoUrl")]
    public string RepoUrl { get; set; } = repoUrl;

    [GraphProperty("resourceName")]
    public string ResourceName => RepoUrl.Split('/').Last();

    [GraphProperty("resourceId")]
    public string ResourceId => RepoUrl;

    public override string GetHashString()
    {
        return GetNodeId();
    }

    public override string GetNodeId()
    {
        return RepoUrl.ToLower().Replace("/", "_").Replace(":", "_");
    }

    public override string GetNodeLabel()
    {
        return "microsoft.source/repository";
    }

    public override string GetResourceType()
    {
        return GetNodeLabel();
    }

    public override string GetSubscriptionId()
    {
        return "";
    }
}
