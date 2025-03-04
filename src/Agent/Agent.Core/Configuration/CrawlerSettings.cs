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
        public string TenantId { get; set; } = "72f988bf-86f1-41af-91ab-2d7cd011db47";
        [Required]
        public string SubscriptionId { get; set; } = string.Empty;
    }
}
