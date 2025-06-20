using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Agent.Evals;

public class ModelGenerationContent
{
    public required string AgentName { get; set; }

    public required ChatMessage[] ModelInput { get; set; }

    public required ChatMessage[] ModelOutput { get; set; }
}

class ModelGenerationContentRaw
{
    public required string AgentName { get; set; }

    public required Message[] ModelInput { get; set; }

    public required Message[] ModelOutput { get; set; }

    internal class Message
    {
        public required string Role { get; set; }
        public required Content[] Contents { get; set; }
    }

    internal class Content
    {
        public required string Type { get; set; }
        public required AIContent Value { get; set; }
    }
}
