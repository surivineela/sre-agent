// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration
{
    public class CrawlerSettings
    {
        [Required]
        public string TenantId { get; set; } = string.Empty;
        // comma delimeted list of resource ids
        public string CrawlRoots { get; set; } = string.Empty;
        // 'system' for system managed identity
        // or resource id of user assigned managed identity
        public string? Identity { get; set; }
        [Required]
        public int MaxParallelism { get; set; } = 4096;
    }
}

