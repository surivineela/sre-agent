// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ManagedServiceIdentities;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class ManagedIdentityCrawler : IResourceCrawler
{
    private readonly ILogger<ManagedIdentityCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly ArmClient _armClient;
    private readonly AzureResourceGraphClient _graphClient;

    public ManagedIdentityCrawler(ILogger<ManagedIdentityCrawler> logger, IGraphDatabaseClient graphDbClient, AzureResourceGraphClient graphClient, ArmClient armClient)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _armClient = armClient;
        _graphClient = graphClient;
    }

    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        var identityNode = (ManagedIdentityNode)node;
        _logger.LogDebug($"Crawling managed identity {identityNode.ResourceId}");

        var identityId = ResourceGroupResource.CreateResourceIdentifier(identityNode.SubscriptionId, identityNode.ResourceGroupName);
        var rgResource = _armClient.GetResourceGroupResource(identityId);
        if (identityNode.IdentityType == ManagedIdentityNode.UserAssignedManagedIdentityType)
        {
            var resp = rgResource.GetUserAssignedIdentity(identityNode.ResourceName);
            if (resp == null || resp.Value == null || !resp.Value.HasData)
            {
                _logger.LogWarning($"Failed to get user assigned managed identity {identityNode.ResourceId}");
                yield break;
            }

            var identity = resp.Value.Data;
            identityNode.Location = identity.Location;
            identityNode.TenantId = identity.TenantId.ToString();
            identityNode.PrincipalId = identity.PrincipalId.ToString();
            identityNode.ClientId = identity.ClientId.ToString();

            await _graphDbClient.AddOrUpdateNodeAsync(identityNode);
        }
        else
        {
            var id = new ResourceIdentifier(identityNode.ResourceId);
            var resp = await _armClient.GetGenericResource(id).GetAsync();
            if (resp == null || resp.Value == null || !resp.Value.HasData)
            {
                _logger.LogWarning($"Failed to get resource {identityNode.ResourceId}");
                yield break;
            }

            var identityResp = await resp.Value.GetSystemAssignedIdentity().GetAsync();
            if (identityResp == null || identityResp.Value == null || !identityResp.Value.HasData)
            {
                _logger.LogWarning($"Failed to get system managed identity {identityNode.ResourceId}");
                yield break;
            }

            var identity = identityResp.Value.Data;
            identityNode.Location = identity.Location;
            identityNode.TenantId = identity.TenantId.ToString();
            identityNode.PrincipalId = identity.PrincipalId.ToString();
            identityNode.ClientId = identity.ClientId.ToString();
            await _graphDbClient.AddOrUpdateNodeAsync(identityNode);
        }

        var principalId = identityNode.PrincipalId;
        var queryResult = await _graphClient.Query([], $"authorizationresources|where properties.principalId == '{principalId}'| project roleId=tostring(properties.roleDefinitionId), scope=tostring(properties.scope)");

        _logger.LogDebug($"Find {queryResult.Count} explicit role assignments on for {identityNode.ResourceId}");
        var jsonObj = JsonSerializer.Deserialize<JsonElement>(queryResult.Data);
        foreach (var item in jsonObj.EnumerateArray())
        {
            var roleId = item.GetProperty("roleId").GetString();
            var scope = item.GetProperty("scope").GetString();

            // TODO: better logic to handle scope
            ArmResourceNode targetResourceNode = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(scope);

            await _graphDbClient.AddOrUpdateNodeAsync(targetResourceNode);

            var edge = new ArmResourceEdge(identityNode.GetNodeId(), targetResourceNode.GetNodeId(), Constants.Relationships.HasRole);
            edge.AddRbacExplicitEdgeProperties()
                .AddOrUpdateEdgeProperty(Constants.RoleAssignmentIdKey, roleId);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            yield return targetResourceNode;
        }
    }
}

