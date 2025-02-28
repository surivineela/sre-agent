using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Configuration
{
    public class AzureSearchSettings
    {
        public string SearchServiceUri { get; set; } = string.Empty;
        public string IndexName { get; set; } = string.Empty;
        public string UserAssignedMIClientId { get; set; } = string.Empty;
        public string SearchApiKeyOverride { get; set; } = string.Empty;
    }
}
