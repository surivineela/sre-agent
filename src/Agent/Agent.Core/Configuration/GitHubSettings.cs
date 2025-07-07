// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration
{
    public class GitHubSettings
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string CallbackUrl { get; set; }
        public string PatTokenOverride { get; set; }
        public string RedirectUriFormat { get; set; }
        public string CustomAgentsRepoPath { get; set; }
    }
}

