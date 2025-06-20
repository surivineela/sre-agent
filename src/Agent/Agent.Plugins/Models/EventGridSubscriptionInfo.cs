// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Models
{
    public class EventGridSubscriptionInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Topic { get; set; }
        public string ProvisioningState { get; set; }
        public string DestinationType { get; set; }
        public string EndpointUrl { get; set; }
        public string MinimumTlsVersion { get; set; }
        public string SubjectBeginsWith { get; set; }
        public string SubjectEndsWith { get; set; }
        public List<string> IncludedEventTypes { get; set; }
        public int? MaxDeliveryAttempts { get; set; }
        public int? EventTimeToLiveInMinutes { get; set; }
    }
}