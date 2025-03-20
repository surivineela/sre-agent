using Agent.Graph.Crawler.Legacy;
using Agent.Graph.Schema;
using Agent.Plugins.Models;

namespace Agent.Plugins
{
    public class MockSubscriptionPlugin : ISubscriptionPlugin
    {
        public async Task<IReadOnlyList<SubscriptionDescriptor>> ListAllSubscriptionsAsync()
        {
            await Task.Yield();

            return [
                new SubscriptionDescriptor("5abde51d-cc72-4bcc-b0d7-3c86b4db2a7c", "Private Test Sub SHGUP"),
                new SubscriptionDescriptor("b5ec1be6-2c6e-4e1c-aa22-5c1c35582489", "Mock Subscription 2")];
        }

        public async Task<IReadOnlyList<AppServiceDescriptor>> ListAppServicesAsync(Guid subscriptionId)
        {
            await Task.Yield();

            return [
                new AppServiceDescriptor("/subscriptions/5abde51d-cc72-4bcc-b0d7-3c86b4db2a7c/resourceGroups/appservices-sre-demo/providers/Microsoft.Web/sites/oa-demo-web-stage",
                "oa-demo-web-stage",
                "WebApp",
                "WestUS2",
                "Standard",
                "Running",
                "appservices-sre-demo")
                ];
        }

        public async Task<InMemoryGraphManager> BuildResourceGraphForAllSubscriptionsAsync()
        {
            await Task.Yield();

            var resources = new List<Resource>
            {
                new Resource
                {
                    Id = "1",
                    Name = "Resource1",
                    Type = "Type1",
                    ChildResources = new List<Resource>
                    {
                        new Resource
                        {
                            Id = "1.1",
                            Name = "ChildResource1",
                            Type = "ChildType1",
                            ChildResources = new List<Resource>()
                        }
                    }
                },
                new Resource
                {
                    Id = "2",
                    Name = "Resource2",
                    Type = "Type2",
                    ChildResources = new List<Resource>()
                }
            };

            var graphManager = new InMemoryGraphManager();

            foreach (var resource in resources)
            {
                var node = new Node(resource.Id, resource.Name, resource.Type);
                graphManager.AddOrUpdateNode(node);

                foreach (var childResource in resource.ChildResources)
                {
                    var childNode = new Node(childResource.Id, childResource.Name, childResource.Type);
                    graphManager.AddOrUpdateNode(childNode);
                    graphManager.AddDirectedEdgeIfNotExists(node, childNode, "contains");
                }
            }
            return graphManager;
        }

        public Task<IReadOnlyList<ContainerAppDescriptor>> ListContainerAppsAsync(Guid subscriptionId)
        {
            throw new NotImplementedException();
        }
    }
}
