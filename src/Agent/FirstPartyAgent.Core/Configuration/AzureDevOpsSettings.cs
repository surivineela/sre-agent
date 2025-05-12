// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Core.Configuration
{
    public class AzureDevOpsSettings
    {
        public bool Enabled { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string SearchEndpoint { get; set; } = string.Empty;
        public string Organization { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string RepositoryId { get; set; } = string.Empty;
        public string RepositoryName { get; set; } = string.Empty;
        public string MainBranchName { get; set; } = string.Empty;
        public string TokenRequestContext { get; set; } = "499b84ac-1321-427f-aa17-267ca6975798/.default";
        public string ManagedIdentityClientId { get; set; } = string.Empty;
        public string PersonalAccessToken { get; set; } = string.Empty;
    }
}

