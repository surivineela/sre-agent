// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;

namespace FirstPartyAgent.Core.Configuration
{
    public class FirstPartyAgentExternalSettings : ExternalSettings
    {
        public ICMAPISettings ICMAPI { get; set; } = new();

        public ICMWorkflowSettings ICMWorkflows { get; set; } = new();

        public KustoSettings Kusto { get; set; } = new();

        public ObserverClientSettings Observer { get; set; }
        public AzureSearchSettings AzureSearch { get; set; }
        public TeamsClientSettings Teams { get; set; }
        public StorageAccountSettings Storage { get; set; }
        public AzureAlertingSettings AzureAlerting { get; set; }
        public AzureDevOpsSettings AzureDevOps { get; set; } = new();
        public TsgCrawlerSettings TsgCrawler { get; set; } = new();
        public DevOpsSetting DevOps { get; set; }
        public ApplensSettings Applens { get; set; } = new();
        public IcmAgentSettings IcmAgent { get; set; }
        public HandoffToAgentSettings HandoffToAgentConfig { get; set; } = new();
    }
}

