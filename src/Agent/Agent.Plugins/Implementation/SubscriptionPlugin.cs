namespace Agent.Plugins
{
    public class SubscriptionPlugin : ISubscriptionPlugin
    {
        public Task<IReadOnlyList<SubscriptionDescriptor>> ListAllSubscriptionsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<AppServiceDescriptor>> ListAppServicesAsync(Guid subscriptionId)
        {
            throw new NotImplementedException();
        }
    }
}
