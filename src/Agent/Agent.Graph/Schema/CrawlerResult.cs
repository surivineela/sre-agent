// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Graph.Schema
{
    public class CrawlerResult
    {
        public bool IsCrawling { get; set; }
        public bool HasCompletedInitialGraphCrawl { get; set; }
        public int CrawledCount { get; set; }
        public int TotalVisibleResources { get; set; }
        public IDictionary<string, object> Properties { get; set; }
    }
}
