using System.Net;
using System.Text.Json;
using Agent.Data.DatabaseClients.GraphDbClient;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ManagedServiceIdentities;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

/// <summary>
/// This crawler does not have prior knowledge
/// It just finds potential arm resource identifier within the payload
/// </summary>
public class GenericArmResourceCrawler : IResourceCrawler
{
    private readonly ILogger _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly bool _crawlLinkedResource = true;

    protected readonly ArmClient _armClient;

    private static HashSet<string> _skipPath = new()
    {
        ".identity", // skip identity section because it is explicitly crawled
    };

    public GenericArmResourceCrawler(ILogger logger, IGraphDatabaseClient dbManager, ArmClient armClient, bool crawlLinkedResource = true)
    {
        _logger = logger;
        _graphDbClient = dbManager;
        _armClient = armClient;
        _crawlLinkedResource = crawlLinkedResource;
    }

    public virtual async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        var armNode = (ArmResourceNode)node;
        _logger.LogDebug($"Crawling generic ARM resource {armNode.ResourceId}");
        if (armNode.ResourceType.Contains("microsoft.containerservice/daemonSet") || armNode.ResourceType.Contains("microsoft.containerservice/deployment"))
        {
            yield break;
        }
        var id = new ResourceIdentifier(armNode.ResourceId);
        if (id == null)
        {
            _logger.LogWarning($"Invalid resource id: {armNode.ResourceId}");
            yield break;
        }

        Response<GenericResource> resp = null;
        try
        {
            resp = await _armClient.GetGenericResource(id).GetAsync();
        }
        catch (RequestFailedException ex)
        {
            if (ex.Status == (int)HttpStatusCode.Unauthorized)
            {
                _logger.LogDebug($"Agent MI does not have permission on {armNode.ResourceId}");
            }
            else if (ex.Status == (int)HttpStatusCode.BadRequest)
            {
                if (ex.ErrorCode == "NoRegisteredProviderFound")
                {
                    _logger.LogDebug($"No registered provider found: {armNode.ResourceId}, {ex}");
                }
                else
                {
                    _logger.LogWarning($"Failed to get resource: {armNode.ResourceId}, {ex}");
                }
            }
            yield break;
        }
        catch (InvalidOperationException ex)
        {
            // Usually this is because some properties linked some non-ARM resources
            // Remove the logs to avoid noises
            _logger.LogDebug($"Invalid node resource type: {armNode.ResourceId}, {ex}");
            yield break;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to get resource: {armNode.ResourceId}, {ex}");
            yield break;
        }

        if (resp == null || resp.Value == null || !resp.Value.HasData)
        {
            _logger.LogWarning($"Failed to get resource: {armNode.ResourceId}");

        }

        var location = resp.Value.Data.Location;
        armNode.Location = location;
        await _graphDbClient.AddOrUpdateNodeAsync(armNode);

        var identity = resp.Value.Data.Identity;
        if (identity != null)
        {
            if (identity.ManagedServiceIdentityType == ManagedServiceIdentityType.SystemAssigned || identity.ManagedServiceIdentityType == ManagedServiceIdentityType.SystemAssignedUserAssigned)
            {
                var identityResp = await resp.Value.GetSystemAssignedIdentity().GetAsync();
                if (resp == null || resp.Value == null || !resp.Value.HasData)
                {
                    _logger.LogWarning($"Failed to get system assigned identity for resource: {armNode.ResourceId}");
                }

                var identityResourceId = resp.Value.Id;
                var resourceId = new ResourceIdentifier(identityResourceId);
                var identityNode = new ManagedIdentityNode(resourceId.ResourceType, identityResourceId, resourceId.SubscriptionId, resourceId.ResourceGroupName, resourceId.Name, ManagedIdentityNode.SystemAssignedManagedIdentityType);
                await _graphDbClient.AddOrUpdateNodeAsync(identityNode);

                yield return identityNode;
            }

            if (identity.UserAssignedIdentities.Count > 0)
            {
                foreach (var uami in identity.UserAssignedIdentities)
                {
                    var identityResourceId = uami.Key;
                    var resourceId = new ResourceIdentifier(identityResourceId);
                    var identityNode = new ManagedIdentityNode(resourceId.ResourceType, identityResourceId, resourceId.SubscriptionId, resourceId.ResourceGroupName, resourceId.Name, ManagedIdentityNode.UserAssignedManagedIdentityType);
                    await _graphDbClient.AddOrUpdateNodeAsync(identityNode);

                    var edge = new ArmResourceEdge(node.GetNodeId(), identityNode.GetNodeId(), Constants.Relationships.HasIdentity);
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);

                    yield return identityNode;
                }
            }
        }

        if (_crawlLinkedResource)
        {
            var jsonObj = JsonSerializer.Deserialize<JsonElement>(resp.Value.Data.Properties);
            foreach (var link in Traverse(jsonObj, "."))
            {
                _logger.LogDebug($"Find linked resource: {link.ResourceId}");
                await _graphDbClient.AddOrUpdateNodeAsync(link);

                var edge = new ArmResourceEdge(node.GetNodeId(), link.GetNodeId(), Constants.Relationships.Linked);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);

                yield return link;
            }
        }
    }

    private IEnumerable<ArmResourceNode> Traverse(JsonElement root, string path)
    {
        if (_skipPath.Contains(path.ToLowerInvariant()))
        {
            yield break;
        }

        switch (root.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in root.EnumerateObject())
                {
                    foreach (var node in Traverse(property.Value, $".{property.Name}"))
                    {
                        yield return node;
                    }
                }
                break;
            case JsonValueKind.Array:
                int idx = 0;
                foreach (var element in root.EnumerateArray())
                {
                    foreach (var node in Traverse(element, $"[{idx}]"))
                    {
                        yield return node;
                    }
                    idx++;
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
