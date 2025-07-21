// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM
{
    public class APIManagementBackendCrawler : GenericArmResourceCrawler
    {
        private readonly ILogger<APIManagementBackendCrawler> _logger;
        private readonly IGraphDatabaseClient _graphDbClient;
        private readonly AzureResourceGraphClient _graphClient;

        public APIManagementBackendCrawler(ILogger<APIManagementBackendCrawler> logger, IGraphDatabaseClient graphDbClient, AzureResourceGraphClient graphClient, ArmClient armClient)
            : base(logger, graphDbClient, armClient, false)
        {
            _logger = logger;
            _graphDbClient = graphDbClient;
            _graphClient = graphClient;
        }

        public override async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
        {
            await foreach (var n in base.Crawl(node))
            {
                yield return n;
            }
            var apimBackendNode = (APIManagementBackendNode)node;

            var armResourceId = apimBackendNode.ArmResourceId;
            if (!string.IsNullOrEmpty(armResourceId)){
                _logger.LogInternalInformation($"Processing API Management Azure Backend Resource ID: {armResourceId}");
                var origNodeId = await _graphDbClient.GetNodeId(armResourceId);
                var apimToOrigBackendEdge = new ArmResourceEdge(apimBackendNode.GetNodeId(), origNodeId, Constants.Relationships.Linked);
                await _graphDbClient.AddOrUpdateEdgeAsync(apimToOrigBackendEdge);
            }
        }
    }
}
