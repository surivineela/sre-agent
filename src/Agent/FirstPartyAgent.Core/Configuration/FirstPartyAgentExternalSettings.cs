// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;

namespace FirstPartyAgent.Core.Configuration
{
    public class FirstPartyAgentExternalSettings : ExternalSettings
    {
        public ICMAPISettings ICMAPI { get; set; } = new();

        public ObserverClientSettings Observer { get; set; } = new();
        public TeamsClientSettings Teams { get; set; } = new();
        public StorageAccountSettings Storage { get; set; } = new();
        public AzureAlertingSettings AzureAlerting { get; set; } = new();
        public AzureDevOpsSettings AzureDevOps { get; set; } = new();
        public DevOpsSetting DevOps { get; set; } = new();
        public IcmAgentSettings IcmAgent { get; set; } = new();
        public HandoffToAgentSettings HandoffToAgentConfig { get; set; } = new();
    }
}

