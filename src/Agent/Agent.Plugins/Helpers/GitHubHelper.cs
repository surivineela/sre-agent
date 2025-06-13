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
            if (string.IsNullOrWhiteSpace(repoUrl))
            {
                throw new ArgumentException("Repository URL cannot be empty.");
            }

            string regexPattern = @"github\.com[/:](?<owner>[\w.-]+)/(?<repo>[\w.-]+)(?:\.git)?";
            string errorMessage = $"Repository URL must be of the form https://github.com/owner/repo-name.git whereas the supplied repoUrl is {repoUrl}";
            if (repoUrl.Contains("/repos/", StringComparison.OrdinalIgnoreCase))
            {
                regexPattern = @"github\.com/repos[/:](?<owner>[\w.-]+)/(?<repo>[\w.-]+)";
                errorMessage = $"Repository URL must be of the form https://github.com/repos/owner/repo-name whereas the supplied repoUrl is {repoUrl}";
            }

            var match = Regex.Match(repoUrl, regexPattern);
            if (!match.Success)
            {
                throw new ArgumentException(errorMessage);
            }

            var owner = match.Groups["owner"].Value;
            var repo = match.Groups["repo"].Value;
            if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                repo = repo.Substring(0, repo.Length - 4); // Remove the .git suffix if present
            }
            return (owner, repo);
        }

        public static (string owner, string repo, long issueNumber) ParseGitHubIssueUrl(string issueUrl, bool throwOnError = true)
        {
            if (string.IsNullOrWhiteSpace(issueUrl) || !issueUrl.Contains("/issues/", StringComparison.OrdinalIgnoreCase))
            {
                if (throwOnError)
                {
                    throw new ArgumentException("Github issue URL cannot be empty.");
                }
                else
                {
                    return (string.Empty, string.Empty, 0);
                }
            }

            string errorMessage = $"GitHub issue URL must be of the form https://github.com/ownerName/repoName/issues/issueNumber whereas the supplied repoUrl is {issueUrl}";
            string regexPattern = @"github\.com[/:](?<owner>[\w.-]+)/(?<repo>[\w.-]+)/issues/(?<issueNumber>\d+)";

            if (issueUrl.Contains("/repos/", StringComparison.OrdinalIgnoreCase))
            {
                regexPattern = @"github\.com/repos[/:](?<owner>[\w.-]+)/(?<repo>[\w.-]+)/issues/(?<issueNumber>\d+)";
                errorMessage = $"GitHub issue URL must be of the form https://api.github.com/repos/ownerName/repoName/issues/issueNumber whereas the supplied repoUrl is {issueUrl}";
            }
            var match = Regex.Match(issueUrl, regexPattern);
            if (!match.Success)
            {
                if (throwOnError)
                {
                    throw new ArgumentException(errorMessage);
                }
                else
                {
                    return (string.Empty, string.Empty, 0);
                }
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

        public static List<string> GetEmbededImageUrls(string content)
        {
            List<string> extractedUrls = new();
            if (string.IsNullOrWhiteSpace(content))
            {
                return extractedUrls;
            }
            // Image urls are of the format https://github.com/user-attachments/assets/GUID. RegEx should match this pattern
            string pattern = @"https:\/\/github\.com\/user-attachments\/assets\/(?<guid>[a-zA-Z0-9\-]+)";

            var regex = new Regex(pattern);
            var matches = regex.Matches(content);

            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    extractedUrls.Add(match.Groups[0].Value);
                }
            }

            return extractedUrls;
        }
    }
}

