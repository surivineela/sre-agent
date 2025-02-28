using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Configuration
{
    public class AzureSettings
    {
        [Required]
        public OpenAISettings OpenAI { get; set; } = new();

        [Required]
        public AppInsightsSettings AppInsights { get; set; } = new();

        [Required]
        public bool OpenSupportTickets { get; set; }

        [Required]
        public CosmosDBSettings CosmosDB { get; set; } = new();

        [Required]
        public DurableTaskSchedulerSettings DTS { get; set; } = new();
    }
}
