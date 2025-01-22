using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperationalAgentRuntime.Models
{
    public class BasicAuthStatus
    {
        public string? ResourceId { get; set; }
        public string? Name { get; set; }
        public string? Location { get; set; }
        public bool FtpBasicAuthAllowed { get; set; }
        public bool ScmBasicAuthAllowed { get; set; }
    }
}
