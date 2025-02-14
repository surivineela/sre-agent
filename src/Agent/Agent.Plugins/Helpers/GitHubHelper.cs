using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
    }
}
