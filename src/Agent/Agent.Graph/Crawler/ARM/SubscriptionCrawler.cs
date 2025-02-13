using Agent.Graph.Schema;
using Azure.Identity;
using Azure.ResourceManager;

namespace Agent.Graph.Crawler.ARM;

public class SubscriptionCrawler
{
    public static async Task<List<Node>> CrawlAllSubscriptions(InMemoryGraphManager inMemoryGraphManager)
    {
        string[] displayNameFilter = ["Container Apps Test Resources", "ruslany", "sanmeht", "yanche", "shgup", "pbatum"];

        var subscriptionList = new List<Node>();
        // Authenticate using DefaultAzureCredential
        var credential = new DefaultAzureCredential();
        // Create an instance of the ArmClient to interact with Azure
        var armClient = new ArmClient(credential);
        // Get all subscriptions
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
