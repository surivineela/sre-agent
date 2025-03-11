using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Graph.Schema;
using Azure.Identity;
using Azure.ResourceManager;

namespace Agent.Graph.Crawler.Legacy
{
    public class SubscriptionCrawler
    {
        public static async Task<List<Node>> CrawlAllSubscriptions(InMemoryGraphManager inMemoryGraphManager)
        {
            string[] displayNameFilter = ["Container Apps Test Resources", "ruslany", "sanmeht", "yanche", "shgup", "pbatum"];

            var subscriptionList = new List<Node>();
            var credential = new DefaultAzureCredential();
            var armClient = new ArmClient(credential);
            await foreach (var subscription in armClient.GetSubscriptions().GetAllAsync())
            {
                if (displayNameFilter != null && displayNameFilter.Length > 0 &&
                    !displayNameFilter.Any(filter => subscription.Data.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var subscriptionNode = new Node(
                    id: subscription.Data.Id,
                    name: subscription.Data.DisplayName,
                    type: "Subscription");
                inMemoryGraphManager.AddOrUpdateNode(subscriptionNode);
                subscriptionList.Add(subscriptionNode);
            }

            return subscriptionList;
        }
    }
}
