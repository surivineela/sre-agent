// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


namespace Agent.Core.Configuration
{
    public class AgentActionADX
    {
        public string ClusterUri { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string FirstPartyAppClientId { get; set; } = string.Empty;
        public string FirstPartyAppTenantId { get; set; } = string.Empty;
        public string FirstPartyAppCertificatePath { get; set; } = string.Empty;
    }
}
