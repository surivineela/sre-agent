// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


namespace Agent.Core.Configuration
{
    public class AgentTraceADX
    {
        public string ClusterUri { get; set; } = "https://sreagent-trace-sec.swedencentral.kusto.windows.net";
        public string DatabaseName { get; set; } = "trace";
        public string TableName { get; set; } = "AgentTrace";
        public string FirstPartyAppClientId { get; set; } = string.Empty;
        public string FirstPartyAppTenantId { get; set; } = string.Empty;
        public string FirstPartyAppCertificatePath { get; set; } = string.Empty;
    }
}
