using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Configuration
{
    public class CrawlerSettings
    {
        [Required]
        public string TenantId { get; set; } = string.Empty;
        public string CrawlRoot { get; set; } = string.Empty;
        // 'system' for system managed identity
        // or resource id of user assigned managed identity
        public string? Identity { get; set; }
        [Required]
        public int MaxParallelism { get; set; } = 4096;
    }
}
