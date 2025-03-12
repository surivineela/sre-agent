using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Configuration
{
    public class MCPSettings
    {
        public string[] Servers { get; set; } = [];
        public int PingIntervalInSeconds { get; set; } = 60;
        public int PingTimeoutInSeconds { get; set; } = 10;
    }
}
