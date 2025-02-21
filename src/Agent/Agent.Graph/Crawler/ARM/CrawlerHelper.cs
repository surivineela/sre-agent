using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Agent.Graph.Schema;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ManagedServiceIdentities;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM
{
    public static class CrawlerHelper
    {
        //public static async IAsyncEnumerable<ManagedIdentityNode> ExtractManagedIdentites(GenericResource resource, ArmResourceNode node, IGraphDatabaseManager dbManager, ILogger logger)
        //{

        //}
    }
}
