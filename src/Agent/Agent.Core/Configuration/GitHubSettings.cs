// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration
{
    public class GitHubSettings
    {
        public string ClientId { get; set; }
        public string PatOverride { get; set; }
        public string ClientSecret { get; set; }
        public string CallbackUrl { get; set; }
        public string OidcAudience { get; set; }
        public string[] AllowedRepositories { get; set; }
    }
}

