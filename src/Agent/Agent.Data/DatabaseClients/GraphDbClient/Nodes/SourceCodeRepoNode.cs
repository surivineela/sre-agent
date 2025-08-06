// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.Attributes;
using Agent.Core.Helpers;

namespace Agent.Data.DatabaseClients.GraphDbClient.Nodes;

public class SourceCodeRepoNode(string repoUrl) : GraphNode(new Dictionary<string, object>() {
            { "nonCrawled", true }
        })
{
    public const string Type = "microsoft.source/repository";
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
        return Type;
    }

    public override string GetResourceType()
    {
        return GetNodeLabel();
    }

    public override void SetResourceKind(string? NewResourceKind)
    {
        return;
    }

    public override string GetResourceKind()
    {
        return ResourceKindHelper.getResourceKind(GetResourceType(), null);
    }

    public override string GetSubscriptionId()
    {
        return "";
    }
}
