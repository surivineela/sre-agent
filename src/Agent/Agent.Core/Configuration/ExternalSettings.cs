using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Configuration
{
    public class ExternalSettings
    {
        public string TeamsEndpoint { get; set; } = string.Empty;
        public GitHubSettings GitHub { get; set; } = new();
    }
}
