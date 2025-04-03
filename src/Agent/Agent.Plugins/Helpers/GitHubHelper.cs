// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;

namespace Agent.Plugins.Helpers
{
    public class GitHubHelper
    {
        public const string ExampleUrl = "https://github.com/owner/repo-name.git";

        public static (string owner, string repo) ParseGitHubUrl(string repoUrl)
        {
            var match = Regex.Match(repoUrl, @"github\.com[/:](?<owner>[\w.-]+)/(?<repo>[\w.-]+)(?:\.git)?");
            if (!match.Success)
            {
                throw new ArgumentException("Invalid GitHub repository URL format");
            }

            return (match.Groups["owner"].Value, match.Groups["repo"].Value);
        }

        public static (string owner, string repo, long issueNumber) ParseGitHubIssueUrl(string issueUrl)
        {
            var match = Regex.Match(issueUrl, @"github\.com[/:](?<owner>[\w.-]+)/(?<repo>[\w.-]+)/issues/(?<issueNumber>\d+)$");
            if (!match.Success)
            {
                throw new ArgumentException("Invalid GitHub issue URL format");
            }
            return (match.Groups["owner"].Value, match.Groups["repo"].Value, long.Parse(match.Groups["issueNumber"].Value));
        }

        public static (string owner, string repo) ParseGitHubDependabotAlertUrl(string issueUrl)
        {
            var match = Regex.Match(issueUrl, @"github\.com[/:](?<owner>[\w.-]+)/(?<repo>[\w.-]+)/dependabot/alerts$");
            if (!match.Success)
            {
                throw new ArgumentException("Invalid GitHub issue URL format");
            }
            return (match.Groups["owner"].Value, match.Groups["repo"].Value);
        }
    }
}

