// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

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

        public string ManagedIdentityClientId { get; set; }

        [Required]
        public CosmosDBSettings CosmosDB { get; set; } = new();

        [Required]
        public DurableTaskSchedulerSettings DTS { get; set; } = new();

        [Required]
        public CrawlerSettings Crawler { get; set; } = new();

        public FederationSettings Federation { get; set; } = new();
    }
}

