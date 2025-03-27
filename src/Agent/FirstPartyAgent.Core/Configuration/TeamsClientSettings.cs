using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Core.Configuration
{
    public class TeamsClientSettings
    {
        public string TeamsEndpoint {  get; set; }
        public string TeamsGroupConversationId { get; set; }
        public bool SendLogsToTeams { get; set; }
    }
}
