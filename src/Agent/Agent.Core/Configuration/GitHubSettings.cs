using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
