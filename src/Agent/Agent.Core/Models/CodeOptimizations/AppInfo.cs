// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models
{
    public class AppInfo
    {
        public required string ResourceId { get; set; }
        public required string SubId { get; set; }
        public required string RoleName { get; set; }
        public string? InstrumentationKey { get; set; }
        public string? AppId { get; set; }
        public GenericArmResourceModel? AppInsightsResource { get; set; }
    }
}
