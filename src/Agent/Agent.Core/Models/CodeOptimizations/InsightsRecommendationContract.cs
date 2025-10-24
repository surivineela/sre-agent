// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models
{
    /// <summary>
    ///  A contract that represents a single insight recommendation.
    /// </summary>
    public sealed class InsightsRecommendationContract
    {
        public string PerformanceIssue { get; set; } = string.Empty;
        public string CurrentCondition { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string ImpactPercent { get; set; } = string.Empty;
        public string PortalLink { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string AppId { get; set; } = string.Empty;
    }
}
