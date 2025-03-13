using Microsoft.Extensions.AI;

namespace Agent.Runtime.MetaAgent;

public interface IAgent
{
    Task<string> ProcessUserMessage(string message, string threadId);
}