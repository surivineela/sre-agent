// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Octokit;

namespace Agent.Plugins.Models
{
    public class GitHubClient
    {
        public Octokit.GitHubClient Client { get; }

        public GitHubClient(GitHubSettings gitHubSettings)
        {
            if (gitHubSettings == null)
            {
                throw new ArgumentNullException(nameof(gitHubSettings), "GitHub settings cannot be null");
            }

            // TODO; Remove this post 3/31 demo
            string? ghToken = Environment.GetEnvironmentVariable("ghtoken");
            if (!string.IsNullOrEmpty(ghToken))
            {
                gitHubSettings.PatOverride = ghToken;
            }

            if (string.IsNullOrEmpty(gitHubSettings.PatOverride))
            {
                throw new ArgumentNullException(nameof(gitHubSettings.PatOverride), "GitHub PAT token cannot be null or empty");
            }

            Client = new Octokit.GitHubClient(new ProductHeaderValue("OperationsAgent"))
            {
                Credentials = new Credentials(gitHubSettings.PatOverride, AuthenticationType.Bearer)
            };
        }
    }
}

