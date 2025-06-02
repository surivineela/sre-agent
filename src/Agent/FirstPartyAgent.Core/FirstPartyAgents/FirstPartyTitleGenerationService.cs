using Microsoft.Extensions.AI;
using Agent.Core.Interfaces;
using Agent.Core.Extensions;
using Agent.Core.Helpers;

namespace FirstPartyAgent.Core.FirstPartyAgents;

public class FirstPartyTitleGenerationService : TitleGenerationService, ITitleGenerationService
{
    public FirstPartyTitleGenerationService(IChatClient chatClient, IThreadRepository threadRepository)
        : base(chatClient, threadRepository)
    {
    }

    public override string GetTitleGenerationSystemPrompt()
    {
        return "This thread is for the Azure SRE Agent. Generate a concise and descriptive title for the conversation, using no more than 6 words. Return only the title text—no quotes or extra formatting. **If an IcM incident ID is provided, format the title exactly as: ICM# <incidentID>**";
    }
}

