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
    }
}
