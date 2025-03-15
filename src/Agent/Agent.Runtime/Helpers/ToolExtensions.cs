using McpDotNet.Client;
using McpDotNet.Extensions.AI;
using McpDotNet.Protocol.Types;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Runtime.Helpers
{
    public static class ToolExtensions
    {
        public static AIFunction ToAIFunction(this Tool tool, IMcpClient client)
        {
            return new McpAIFunction(tool, client);
        }
    }
}
