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

            Client = new Octokit.GitHubClient(new ProductHeaderValue("OperationsAgent"))
            {
            };
        }
    }
}

