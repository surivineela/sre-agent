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
        public string IdentityClientId { get; set; } = string.Empty;
        [Required]
        public int MaxParallelism { get; set; } = 4096;
    }
}
