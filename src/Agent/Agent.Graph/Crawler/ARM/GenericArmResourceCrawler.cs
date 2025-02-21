using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ManagedServiceIdentities;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM
{
    /// <summary>
    /// This crawler does not have prior knowledge
    /// It just finds potential arm resource indentifier within the payload
    /// </summary>
    public class GenericArmResourceCrawler : IArmResourceCrawler
    {
        private readonly ILogger _logger;
        private readonly IGraphDatabaseManager _dbManager;
        private readonly ArmClient _armClient;
        private readonly bool _crawlLinkedResource = true;

        public GenericArmResourceCrawler(ILogger logger, IGraphDatabaseManager dbManager, bool crawlLinkedResource = true)
        {
            _logger = logger;
            _dbManager = dbManager;
            _armClient = new ArmClient(new DefaultAzureCredential());
            _crawlLinkedResource = crawlLinkedResource;
        }

        public virtual async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node)
        {
            _logger.LogInformation($"Crawling generic ARM resource {node.ResourceId}");
            if(node.ResourceType.Contains("Microsoft.ContainerService/DaemonSet") || node.ResourceType.Contains("Microsoft.ContainerService/Deployment"))
            {
                yield break;
            }
            var id = new ResourceIdentifier(node.ResourceId);
            if (id == null)
            {
                _logger.LogWarning($"Invalid resource id: {node.ResourceId}");
                yield break;
            }

            Response<GenericResource> resp = null;
            try
            {
                // TODO
                // /subscriptions/ea2aa16c-c257-4359-aaea-ff2b0f3b3d10/resourceGroups/zhenqxu-rg/providers/Microsoft.Network/virtualNetworks/zhenqxu-vnet-ncu/subnets/zhenqxu-wpenv-ncu-2
                // Invalid resource type Microsoft.Network/virtualNetworks/subnets
                resp = await _armClient.GetGenericResource(id).GetAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to get resource: {node.ResourceId}, {ex}");
                yield break;
            }
            
            if (resp == null || resp.Value == null || !resp.Value.HasData)
            {
                _logger.LogWarning($"Failed to get resource: {node.ResourceId}");
                
            }

            var identity = resp.Value.Data.Identity;
            if (identity != null)
            {
                if (identity.ManagedServiceIdentityType == ManagedServiceIdentityType.SystemAssigned || identity.ManagedServiceIdentityType == ManagedServiceIdentityType.SystemAssignedUserAssigned)
                {
                    var identityResp = await resp.Value.GetSystemAssignedIdentity().GetAsync();
                    if (resp == null || resp.Value == null || !resp.Value.HasData)
                    {
                        _logger.LogWarning($"Failed to get system assigned identity for resource: {node.ResourceId}");
                    }

                    var identityResourceId = resp.Value.Id;
                    var resourceId = new ResourceIdentifier(identityResourceId);
                    var identityNode = new ManagedIdentityNode(resourceId.ResourceType, identityResourceId, resourceId.SubscriptionId, resourceId.ResourceGroupName, resourceId.Name, ManagedIdentityNode.SystemAssignedManagedIdentityType);
                    await _dbManager.AddOrUpdateNodeAsync(identityNode.GetNodeLabel(), identityNode.GetNodeId(), identityNode.GetResourceType(), identityNode.GetNodeProperties());

                    yield return identityNode;
                }

                if (identity.UserAssignedIdentities.Count > 0)
                {
                    foreach (var uami in identity.UserAssignedIdentities)
                    {
                        var identityResourceId = uami.Key;
                        var resourceId = new ResourceIdentifier(identityResourceId);
                        var identityNode = new ManagedIdentityNode(resourceId.ResourceType, identityResourceId, resourceId.SubscriptionId, resourceId.ResourceGroupName, resourceId.Name, ManagedIdentityNode.UserAssignedManagedIdentityType);
                        await _dbManager.AddOrUpdateNodeAsync(identityNode.GetNodeLabel(), identityNode.GetNodeId(), identityNode.GetResourceType(), identityNode.GetNodeProperties());
                        await _dbManager.AddEdgeIfNotExistsAsync(node.GetNodeId(), identityNode.GetNodeId(), "HAS_IDENTITY");

                        yield return identityNode;
                    }
                }
            }

            if (_crawlLinkedResource)
            {
                var jsonObj = JsonSerializer.Deserialize<JsonElement>(resp.Value.Data.Properties);
                foreach (var link in Tranverse(jsonObj))
                {
                    _logger.LogInformation($"Find linked resource: {link.ResourceId}");
                    await _dbManager.AddOrUpdateNodeAsync(link.GetNodeLabel(), link.GetNodeId(), link.GetResourceType(), link.GetNodeProperties());
                    await _dbManager.AddEdgeIfNotExistsAsync(node.GetNodeId(), link.GetNodeId(), "LINKED");
                    yield return link;
                }
            }
        }

        private IEnumerable<ArmResourceNode> Tranverse(JsonElement root)
        {
            switch (root.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in root.EnumerateObject())
                    {
                        foreach (var node in Tranverse(property.Value))
                        {
                            yield return node;
                        }
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var element in root.EnumerateArray())
                    {
                        foreach (var node in Tranverse(element))
                        {
                            yield return node;
                        }
                    }
                    break;
                case JsonValueKind.String:
                    {
                        ArmResourceNode node = null;

                        try
                        {
                            // "/" means tenant
                            if (root.GetString() != "/")
                            {
                                var id = new ResourceIdentifier(root.GetString());
                                node = new ArmResourceNode(id.ResourceType, root.GetString(), id.SubscriptionId, id.ResourceGroupName, id.Name);
                            }
                        }
                        catch { }

                        if (node != null)
                        {
                            yield return node;
                        }
                        break;
                    }
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                default:
                    break;
            }

            yield break;
        }
    }
}
