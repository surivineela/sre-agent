using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM
{
    /// <summary>
    /// This crawler does not have prior knowledge
    /// It just finds potential arm resource indentifier within the payload
    /// </summary>
    public class GenericArmResourceCrawler : IArmResourceCrawler
    {
        private readonly ILogger<GenericArmResourceCrawler> _logger;
        private readonly IGraphDatabaseManager _dbManager;
        private readonly ArmClient _armClient;

        public GenericArmResourceCrawler(ILogger<GenericArmResourceCrawler> logger, IGraphDatabaseManager dbManager)
        {
            _logger = logger;
            _dbManager = dbManager;
            _armClient = new ArmClient(new DefaultAzureCredential());
        }

        public async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node)
        {
            _logger.LogInformation($"Crawling generic ARM resource {node.ResourceId}");
            var id = new ResourceIdentifier(node.ResourceId);
            if (id == null)
            {
                _logger.LogWarning($"Invalid resource id: {node.ResourceId}");
                yield break;
            }

            var resp = await _armClient.GetGenericResource(id).GetAsync();
            if (resp == null || resp.Value == null || !resp.Value.HasData)
            {
                _logger.LogWarning($"Failed to get resource: {node.ResourceId}");
                yield break;
            }

            var jsonObj = JsonSerializer.Deserialize<JsonElement>(resp.Value.Data.Properties);
            foreach(var link in Tranverse(jsonObj))
            {
                _logger.LogInformation($"Find linked resource: {link.ResourceId}");
                await _dbManager.AddOrUpdateNodeAsync(link.GetNodeLabel(), link.GetNodeId(), link.GetResourceType(), link.GetNodeProperties());
                await _dbManager.AddEdgeIfNotExistsAsync(node.GetNodeId(), link.GetNodeId(), "LINKED");
                yield return link;
            }

            yield break;
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
                            var id = new ResourceIdentifier(root.GetString());
                            node = new ArmResourceNode(id.ResourceType, root.GetString(), id.SubscriptionId, id.ResourceGroupName, id.Name);
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
