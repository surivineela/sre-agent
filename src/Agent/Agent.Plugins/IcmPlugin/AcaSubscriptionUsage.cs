// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.IcmPlugin
{
    public class AcaSubscriptionUsage
    {
        public string? SubscriptionId { get; set; }
        public string? NumberOfEnvironments { get; set; }
        public string? NumberOfContainerApps { get; set; }
        public string? NumberOfJobs { get; set; }
        public string? TrustLevel { get; set; }
    }
}
