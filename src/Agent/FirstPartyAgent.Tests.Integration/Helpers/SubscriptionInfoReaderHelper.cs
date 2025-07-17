// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text.Json;

namespace FirstPartyAgent.Tests.End2End.Helpers
{
    public class SubscriptionInfoReaderHelper
    {
        private readonly List<Subscription> _subscriptions;

        public SubscriptionInfoReaderHelper()
        {
            string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            string filePath = Path.Combine(assemblyDirectory, @"..\..\..\TestCases\Subscriptions.json");
            var json = File.ReadAllText(filePath);
            var subscriptionData = JsonSerializer.Deserialize<SubscriptionData>(json);
            _subscriptions = subscriptionData?.Subscriptions ?? new List<Subscription>();
        }

        public string? GetOfferTypeBySubscriptionId(string subscriptionId)
        {
            var subscription = _subscriptions.FirstOrDefault(s => s.SubscriptionId == subscriptionId);
            return subscription?.OfferType;
        }

        public string? GetQoutaIdBySubscriptionId(string subscriptionId)
        {
            var subscription = _subscriptions.FirstOrDefault(s => s.SubscriptionId == subscriptionId);
            return subscription?.QuotaId;
        }
    }

    internal class Subscription
    {
        public required string SubscriptionId { get; set; }
        public required string OfferType { get; set; }
        public required string QuotaId { get; set; }
    }

    internal class SubscriptionData
    {
        public required List<Subscription> Subscriptions { get; set; }
    }
}
