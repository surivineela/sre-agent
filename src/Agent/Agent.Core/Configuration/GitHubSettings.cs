// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration
{
    public class GitHubSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string CallbackUrl { get; set; } = string.Empty;
        public string PatTokenOverride { get; set; } = string.Empty;
        public string RedirectUriFormat { get; set; } = string.Empty;
        public string CustomAgentsRepoPath { get; set; } = string.Empty;
    }
}

