// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Octokit;
using Octokit.Internal;

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

            if (!string.IsNullOrEmpty(gitHubSettings.PatTokenOverride) && gitHubSettings.PatTokenOverride != "replace")
            {
                var githubCredentialStore = new InMemoryCredentialStore(credentials: new Octokit.Credentials(gitHubSettings.PatTokenOverride));
                Client = new Octokit.GitHubClient(new ProductHeaderValue("OperationsAgent"), credentialStore: githubCredentialStore)
                {
                };
            }
            else
            {
                Client = new Octokit.GitHubClient(new ProductHeaderValue("OperationsAgent"))
                {
                };
            }
        }
    }
}

