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
        public CosmosDBSettings CosmosDB { get; set; } = new();

        [Required]
        public CrawlerSettings Crawler { get; set; } = new();

        [Required]
        public ActionSettings Action { get; set; } = new();

        [Required]
        public AgentSpaceProxySettings AgentSpaceProxy { get; set; } = new();

        public FederationSettings Federation { get; set; } = new();

        public KustoFirstPartyAppSettings Kusto { get; set; } = new();

        public SearchEndpointSettings SearchEndpoint { get; set; } = new();

        public StorageSettings StorageSettings { get; set; } = new();

        public SessionPoolSettings SessionPool { get; set; } = new();

        public IndexingSettings Indexing { get; set; } = new();

        public KustoClusterConfiguration AgentTraceKusto { get; set; } = new KustoClusterConfiguration
        {
            ClusterUri = "https://sreagent-trace-test.swedencentral.kusto.windows.net",
            DatabaseName = "trace",
        };

        public EventHubConfiguration AgentTraceEventHub { get; set; } = new()
        {
            EventHubName = "agent-trace",
        };

        public EmergingIssueSettings EmergingIssue { get; set; } = new();

        public ToolOutputSettings ToolOutputSettings { get; set; } = new();
    }
}

