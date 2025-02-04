using Microsoft.Extensions.Configuration;
using Octokit;

namespace OperationalAgentCore.Models
{
    public class GitHubClient
    {
        internal Octokit.GitHubClient Client { get; }

        public GitHubClient(IConfiguration configuration)
        {

            GitHubSettings? gitHubSettings = configuration.GetSection("Azure")?.Get<AzureSettings>()?.Github;

            if (gitHubSettings == null)
            {
                throw new ArgumentNullException(nameof(gitHubSettings), "GitHub settings cannot be null");
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
