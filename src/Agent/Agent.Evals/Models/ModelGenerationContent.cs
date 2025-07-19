using Microsoft.Extensions.AI;

namespace Agent.Evals;

public class ModelGenerationContent
{
    public required string AgentName { get; set; }

    public required ChatMessage[] ModelInput { get; set; }

    public required ChatMessage[] ModelOutput { get; set; }
}

internal class ModelGenerationContentRaw
{
    public required string AgentName { get; set; }

    public required Message[] ModelInput { get; set; }

    public required Message[] ModelOutput { get; set; }

    internal class Message
    {
        public required string Role { get; set; }
        public required Content[] Contents { get; set; }

        public ChatMessage ToChatMessage()
        {
            return new ChatMessage
            {
                Role = new ChatRole(Role),
                Contents = Contents.Select(c => c.Value).ToList()
            };
        }
    }

    internal class Content
    {
        public required string Type { get; set; }
        public required AIContent Value { get; set; }
    }
}
